using Microsoft.EntityFrameworkCore;

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

public sealed class TitlelessEpisodeSkip
{
    private const int RuvProgramId = 77;
    private const string EpisodeId = "ep-tls";
    private const string ProgramName = "Test Program";

    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public TitlelessEpisodeSkip()
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

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext, ILogger<RuvEpisodesSyncJob> logger) => new(
        logger,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task WhenEpisodeTitleContainsNewline_LoggedTitleHasNewlineStripped()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(RuvProgramId)
                .WithName(ProgramName)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        RuvTvEpisode episodeWithNewlineTitle = CreateRuvTvEpisode("ep-lf", title: "Title\nFake log line");
        RuvTvProgram apiResponse = CreateRuvTvProgram(RuvProgramId, episodes: [episodeWithNewlineTitle]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        CapturingLogger<RuvEpisodesSyncJob> logger = new();

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, logger);

        // Act
        await sut.Execute(null!);

        // Assert
        logger.Entries.ShouldContain(e =>
            e.LogLevel == LogLevel.Information
            && e.Message.Contains("Added RÚV episode"),
            "the added-episode log entry must exist");

        logger.Entries.ShouldAllBe(e => !e.Message.Contains('\n'),
            "no log entry should contain an embedded newline from externally-sourced data");
    }

    [Fact]
    public async Task WhenEpisodeTitleIsNull_LogsSkipAtInformation()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(RuvProgramId)
                .WithName(ProgramName)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        RuvTvEpisode titlelessEpisode = CreateRuvTvEpisode(EpisodeId, title: null);
        RuvTvProgram apiResponse = CreateRuvTvProgram(RuvProgramId, episodes: [titlelessEpisode]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        CapturingLogger<RuvEpisodesSyncJob> logger = new();

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, logger);

        // Act
        await sut.Execute(null!);

        // Assert
        logger.Entries.ShouldContain(e =>
            e.LogLevel == LogLevel.Information
            && e.Message.Contains(EpisodeId)
            && e.Message.Contains(ProgramName));
    }

    [Fact]
    public async Task WhenEpisodeTitleIsNull_DoesNotPersistEpisode()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(RuvProgramId)
                .WithName(ProgramName)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        RuvTvEpisode titlelessEpisode = CreateRuvTvEpisode(EpisodeId, title: null);
        RuvTvProgram apiResponse = CreateRuvTvProgram(RuvProgramId, episodes: [titlelessEpisode]);

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, new CapturingLogger<RuvEpisodesSyncJob>());

        // Act
        await sut.Execute(null!);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        episodes.ShouldBeEmpty();
    }

    private static RuvTvEpisode CreateRuvTvEpisode(string id, string? title) => new(
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
        Slug: "titleless-episode",
        Description: ["No title here"],
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
        Title: ProgramName,
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

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
