using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

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

public sealed class EpisodeRemoval
{
    private const int RuvProgramId = 42;
    private const string KeptEpisodeId = "ep-1";
    private const string RemovedEpisodeId = "ep-2";

    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public EpisodeRemoval()
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
    public async Task RemovesEpisode_WhenNoLongerInRuvApiResponse()
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
                id: KeptEpisodeId,
                uri: new Uri("http://ruv.is/ep1"),
                title: "Episode 1",
                description: "First",
                firstRun: DateTime.UtcNow,
                duration: TimeSpan.FromMinutes(30));

            program.TryAddEpisode(
                id: RemovedEpisodeId,
                uri: new Uri("http://ruv.is/ep2"),
                title: "Episode 2",
                description: "Second",
                firstRun: DateTime.UtcNow,
                duration: TimeSpan.FromMinutes(30));

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        RuvTvEpisode keptApiEpisode = CreateRuvTvEpisode(KeptEpisodeId, "Episode 1");

        RuvTvProgram apiResponse = CreateRuvTvProgram(
            RuvProgramId,
            episodes: [keptApiEpisode]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(null!);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> remaining = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        remaining.Count.ShouldBe(1);
        remaining[0].RuvId.ShouldBe(KeptEpisodeId);
    }

    private static RuvTvEpisode CreateRuvTvEpisode(string id, string title) => new(
        Id: id,
        Number: 1,
        SeriesId: RuvProgramId,
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
