using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

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

public sealed class PerProgramIsolation
{
    private const int GeisliRuvId = 99;
    private const int Program1RuvId = 101;
    private const int Program2RuvId = 102;
    private const string GeisliName = "Geisli";
    private const string Program1Name = "Forrit Eitt";
    private const string Program2Name = "Forrit Tvö";
    private const string TitlelessEpisodeId = "ep-null";
    private const string ConflictingEpisodeId = "ep-p1x";
    private const string Program2EpisodeId = "ep-p2-1";

    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public PerProgramIsolation()
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
    public async Task WhenSingleTitlelessEpisode_ProgramRefreshesCleanlyWithZeroEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(GeisliRuvId)
                .WithName(GeisliName)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        RuvTvEpisode titlelessEpisode = CreateRuvTvEpisode(TitlelessEpisodeId, title: null);
        RuvTvProgram apiResponse = CreateRuvTvProgram(GeisliRuvId, GeisliName, episodes: [titlelessEpisode]);

        _ruv.GetProgramAsync(GeisliRuvId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(GeisliRuvId, GeisliName);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(null!);

        // Assert
        _syncQueue.Items.ShouldBeEmpty();

        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>()
            .ToListAsync(cancellationToken);
        episodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenRuvClientThrowsForFirstProgram_FirstIsMarkedComplete_SecondStillProcesses()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program1 = new RuvProgramBuilder()
                .WithRuvId(Program1RuvId)
                .WithName(Program1Name)
                .WithMultipleEpisodes()
                .Build();

            RuvProgram program2 = new RuvProgramBuilder()
                .WithRuvId(Program2RuvId)
                .WithName(Program2Name)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program1);
            seedContext.Set<RuvProgram>().Add(program2);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        _ruv.GetProgramAsync(Program1RuvId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("RÚV unavailable"));

        RuvTvEpisode episode2 = CreateRuvTvEpisode(Program2EpisodeId, title: "Episode 1");
        RuvTvProgram apiResponse2 = CreateRuvTvProgram(Program2RuvId, Program2Name, episodes: [episode2]);
        _ruv.GetProgramAsync(Program2RuvId, Arg.Any<CancellationToken>())
            .Returns(apiResponse2);

        _syncQueue.Enqueue(Program1RuvId, Program1Name);
        _syncQueue.Enqueue(Program2RuvId, Program2Name);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(null!);

        // Assert — no item stuck in Processing; queue fully drained
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — program2's episode was persisted
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>()
            .ToListAsync(cancellationToken);
        episodes.ShouldContain(e => e.RuvId == Program2EpisodeId);
    }

    [Fact]
    public async Task WhenSaveChangesFailsForFirstProgram_ChangeTrackerIsCleared_SecondProgramSavesItsOwnChangesOnly()
    {
        // Arrange
        // Seed program1 with one existing episode. The RÚV API will return a new episode for
        // program1 whose RuvId conflicts with a row we raw-insert into the DB after seeding.
        // That causes SaveChanges to fail with a unique constraint violation. The per-program
        // catch block must call ChangeTracker.Clear() before continuing, otherwise program1's
        // pending-insert state carries over into program2's SaveChanges and causes a second failure.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program1 = new RuvProgramBuilder()
                .WithRuvId(Program1RuvId)
                .WithName(Program1Name)
                .WithMultipleEpisodes()
                .Build();

            RuvProgram program2 = new RuvProgramBuilder()
                .WithRuvId(Program2RuvId)
                .WithName(Program2Name)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program1);
            seedContext.Set<RuvProgram>().Add(program2);
            await seedContext.SaveChangesAsync(cancellationToken);

            // Raw-insert an episode with ConflictingEpisodeId against program1's surrogate key.
            // When the job tries to add an episode with the same RuvId, SaveChanges will fail
            // with a UNIQUE constraint violation on the episodes.ruv_id index.
            await seedContext.Database.ExecuteSqlAsync(
                $"""
                INSERT INTO episodes (ruv_id, uri, title, description, first_run, duration_seconds, lookup_count, program_id)
                SELECT {ConflictingEpisodeId}, 'http://ruv.is/conflict', 'Conflicting Episode', 'desc', datetime('now'), 1800, 0, id
                FROM programs WHERE ruv_id = {Program1RuvId}
                """,
                cancellationToken);
        }

        // The RÚV API returns an episode with ConflictingEpisodeId for program1 — this will
        // try to add a duplicate, causing SaveChanges to fail on the unique RuvId index.
        RuvTvEpisode conflictEpisode = CreateRuvTvEpisode(ConflictingEpisodeId, title: "Conflict Episode");
        RuvTvProgram apiResponse1 = CreateRuvTvProgram(Program1RuvId, Program1Name, episodes: [conflictEpisode]);
        _ruv.GetProgramAsync(Program1RuvId, Arg.Any<CancellationToken>())
            .Returns(apiResponse1);

        RuvTvEpisode episode2 = CreateRuvTvEpisode(Program2EpisodeId, title: "Episode 1");
        RuvTvProgram apiResponse2 = CreateRuvTvProgram(Program2RuvId, Program2Name, episodes: [episode2]);
        _ruv.GetProgramAsync(Program2RuvId, Arg.Any<CancellationToken>())
            .Returns(apiResponse2);

        _syncQueue.Enqueue(Program1RuvId, Program1Name);
        _syncQueue.Enqueue(Program2RuvId, Program2Name);

        // Use ONE shared dbContext across both iterations — same as what the job does
        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(null!);

        // Assert — no item stuck in Processing
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — program2's episode is persisted (ChangeTracker.Clear() allowed program2 to save cleanly)
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>()
            .ToListAsync(cancellationToken);
        episodes.ShouldContain(e => e.RuvId == Program2EpisodeId,
            "program2's episode should be saved; ChangeTracker.Clear() must have isolated program1's failure");
    }

    private static RuvTvEpisode CreateRuvTvEpisode(string id, string? title) => new(
        Id: id,
        Number: 1,
        SeriesId: Program2RuvId,
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

    private static RuvTvProgram CreateRuvTvProgram(int id, string name, IReadOnlyList<RuvTvEpisode> episodes) => new(
        LastUpdated: DateTimeOffset.UtcNow,
        Id: id,
        Title: name,
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
