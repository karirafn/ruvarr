using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class ChangedTitleSync
{
    // "Skjaldbo\u0308kustra\u0301kur" is "Skjaldbokustrakur" in NFD form:
    // o+U+0308 (combining diaeresis) = NFD-o-umlaut, a+U+0301 (combining acute) = NFD-a-acute.
    // \uXXXX escapes are used so raw NFD bytes never appear in source files.
    private const string NfdCorrectedTitle = "Skjaldbo\u0308kustra\u0301kur";
    private const string LegacyStoredTitle = "Skjaldbokustrakur";

    private const int RuvProgramId = 55;
    private const string LinklessEpisodeId = "ep-ln1";
    private const string LinkedEpisodeId = "ep-lk1";

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public ChangedTitleSync()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Series>());
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MissingEpisode>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options,
        _serviceProvider);

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvEpisodesSyncJob>.Instance,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task WhenRuvServesUpdatedTitleForLinklessEpisode_TitleOverwrittenAndLookupBackoffReset()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(RuvProgramId)
                .WithMultipleEpisodes()
                .Build();

            // Seed the episode with its initial (ASCII) title through TryAddEpisode so the
            // program owns it and EF tracks it correctly.
            program.TryAddEpisode(
                id: LinklessEpisodeId,
                uri: new Uri("http://ruv.is/ep-ln1"),
                title: LegacyStoredTitle,
                description: "Some description",
                firstRun: DateTime.UtcNow,
                duration: TimeSpan.FromMinutes(30));

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);

            // The "stored title differs from served title" state cannot be reached through the
            // production add path: TryAddEpisode early-returns on a matching RuvId, so a second
            // call with the corrected title would update via UpdateTitle immediately rather than
            // leaving the legacy title in place. Stamp the backoff state directly on the row to
            // simulate an episode that accumulated lookup attempts before the NFD title correction.
            await seedContext.Database.ExecuteSqlAsync(
                $"UPDATE episodes SET lookup_count = 3, next_lookup = datetime('now', '+7 days') WHERE ruv_id = {LinklessEpisodeId}",
                cancellationToken);
        }

        RuvTvEpisode apiEpisode = CreateRuvTvEpisode(RuvProgramId, LinklessEpisodeId, title: NfdCorrectedTitle);
        RuvTvProgram apiResponse = CreateRuvTvProgram(RuvProgramId, episodes: [apiEpisode]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .SingleAsync(e => e.RuvId == LinklessEpisodeId, cancellationToken);

        episode.ShouldSatisfyAllConditions(
            () => episode.Title.ShouldBe(NfdCorrectedTitle),
            () => episode.LookupCount.ShouldBe(0),
            () => episode.NextLookup.ShouldBeNull());
    }

    [Fact]
    public async Task WhenRuvServesUpdatedTitleForLinkedEpisode_TitleOverwrittenButLinksAndLookupStatePreserved()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(RuvProgramId)
                .WithMultipleEpisodes()
                .Build();

            program.TryAddEpisode(
                id: LinkedEpisodeId,
                uri: new Uri("http://ruv.is/ep-lk1"),
                title: LegacyStoredTitle,
                description: "Some description",
                firstRun: DateTime.UtcNow,
                duration: TimeSpan.FromMinutes(30));

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);

            // Attach a TVDB link via the production Match path so the episode has linked episodes.
            RuvEpisode seededEpisode = await seedContext.Set<RuvEpisode>()
                .SingleAsync(e => e.RuvId == LinkedEpisodeId, cancellationToken);
            seededEpisode.Match(tvdbId: 7001, season: 1, episode: 1, isMissing: false);
            seededEpisode.ClearDomainEvents();
            await seedContext.SaveChangesAsync(cancellationToken);

            // The "stored title differs from served title" state cannot be reached through the
            // production add path (see linkless test for explanation). Stamp the legacy title
            // directly to represent an episode that was matched before the NFD correction arrived.
            await seedContext.Database.ExecuteSqlAsync(
                $"UPDATE episodes SET title = {LegacyStoredTitle} WHERE ruv_id = {LinkedEpisodeId}",
                cancellationToken);
        }

        RuvTvEpisode apiEpisode = CreateRuvTvEpisode(RuvProgramId, LinkedEpisodeId, title: NfdCorrectedTitle);
        RuvTvProgram apiResponse = CreateRuvTvProgram(RuvProgramId, episodes: [apiEpisode]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .Include(e => e.TvdbEpisodes)
            .SingleAsync(e => e.RuvId == LinkedEpisodeId, cancellationToken);

        episode.ShouldSatisfyAllConditions(
            () => episode.Title.ShouldBe(NfdCorrectedTitle),
            () => episode.TvdbEpisodes.Count.ShouldBe(1),
            () => episode.TvdbEpisodes[0].TvdbId.ShouldBe(7001),
            () => episode.Matched.ShouldNotBeNull(),
            () => episode.LookupCount.ShouldBe(0),
            () => episode.NextLookup.ShouldBeNull());
    }

    private static RuvTvEpisode CreateRuvTvEpisode(int seriesId, string id, string? title) => new(
        Id: id,
        Number: 1,
        SeriesId: seriesId,
        FirstRun: DateTime.UtcNow,
        FileExpires: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
        Rating: 0,
        Duration: 1800,
        DurationFriendly: "30 min",
        Event: 0,
        Title: title,
        Slug: "test-episode",
        Description: ["Test description"],
        ImageRenditions: new RuvImageRenditions([]),
        Image: new Uri("http://ruv.is/image.jpg"),
        ImageOg: new Uri("http://ruv.is/image-og.jpg"),
        Scope: "ruv",
        SubtitlesUrl: new Uri("http://ruv.is/subs.vtt"),
        Subtitles: new RuvSubtitles(new Uri("http://ruv.is/subs-is.vtt")),
        OpenSubtitles: false,
        ClosedSubtitles: false,
        AutoSubtitles: false,
        File: new Uri("http://ruv.is/stream.m3u8"),
        Temp: new RuvTemp("file.mp4", "folder"),
        Clips: [],
        Files: new RuvFiles(new RuvVodmp4("file.mp4", "folder", "file", false, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), 0, "0", "mp4", "ruv")),
        CreditPoint: 0);

    private static RuvTvProgram CreateRuvTvProgram(int id, IReadOnlyList<RuvTvEpisode> episodes) => new(
        LastUpdated: DateTimeOffset.UtcNow,
        Id: id,
        Title: "Test Program",
        ForeignTitle: "Test",
        Slug: "test-program",
        ImageRenditions: new RuvImageRenditions([]),
        Image: null,
        ImageOg: new Uri("http://ruv.is/og.jpg"),
        PortraitImageRenditions: new RuvPortraitImageRenditions([]),
        PortraitImage: new Uri("http://ruv.is/portrait.jpg"),
        Description: ["Test description"],
        Format: "tv",
        Categories: [],
        Division: "test",
        MultipleEpisodes: true,
        Episodes: episodes,
        ReverseEpisodeOrder: false,
        WebAvailableEpisodes: episodes.Count,
        VodAvailableEpisodes: episodes.Count,
        PodcastVailableEpisodes: 0,
        WebLatestDate: DateTime.UtcNow,
        Channel: "ruv",
        WebPlayerUrl: new Uri("http://ruv.is/player"));
}
