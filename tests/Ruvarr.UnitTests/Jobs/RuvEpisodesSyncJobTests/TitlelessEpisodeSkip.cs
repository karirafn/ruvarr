using Microsoft.EntityFrameworkCore;

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

public sealed class TitlelessEpisodeSkip
{
    private const int RuvProgramId = 77;
    private const string EpisodeId = "ep-tls";
    private const string ProgramName = "Test Program";

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public TitlelessEpisodeSkip()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(Array.Empty<Series>()));
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

        RuvTvEpisode episodeWithNewlineTitle = new RuvTvEpisodeBuilder()
            .WithId("ep-lf")
            .WithTitle("Title\nFake log line")
            .Build();

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithTitle(ProgramName)
            .WithEpisodes([episodeWithNewlineTitle])
            .Build();

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        CapturingLogger<RuvEpisodesSyncJob> logger = new();

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, logger);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

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

        RuvTvEpisode titlelessEpisode = new RuvTvEpisodeBuilder()
            .WithId(EpisodeId)
            .WithTitle(null)
            .Build();

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithTitle(ProgramName)
            .WithEpisodes([titlelessEpisode])
            .Build();

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        CapturingLogger<RuvEpisodesSyncJob> logger = new();

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, logger);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

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

        RuvTvEpisode titlelessEpisode = new RuvTvEpisodeBuilder()
            .WithId(EpisodeId)
            .WithTitle(null)
            .Build();

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithTitle(ProgramName)
            .WithEpisodes([titlelessEpisode])
            .Build();

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);

        _syncQueue.Enqueue(RuvProgramId, ProgramName);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext, new CapturingLogger<RuvEpisodesSyncJob>());

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        episodes.ShouldBeEmpty();
    }

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
