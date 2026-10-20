using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

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

/// <summary>
/// Verifies that the lease structurally drains the sync queue when the client boundary throws any
/// exception (defence-in-depth). The lease carries the item to completion so no item is left stuck
/// in Processing state, regardless of how the failure surfaces.
/// </summary>
public sealed class TransientTimeoutDrainsQueue
{
    private const int Program1RuvId = 301;
    private const string Program1Name = "First Program";
    private const int Program2RuvId = 302;
    private const string Program2Name = "Second Program";
    private const string Program2EpisodeId = "ep-p2-drain";

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public TransientTimeoutDrainsQueue()
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

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvEpisodesSyncJob>.Instance,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task WhenFirstProgramThrowsTaskCanceledException_QueueDrained_SecondProgramEpisodesPersisted()
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

        // Program1: throws TaskCanceledException with an uncancelled context token —
        // this is the transient timeout pattern from HttpClient that must not escape as
        // cooperative cancellation.
        _ruv.GetProgramAsync(Program1RuvId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("timeout", new TimeoutException()));

        // Program2: returns success so we can prove it still ran.
        RuvTvEpisode program2Episode = new RuvTvEpisodeBuilder()
            .WithId(Program2EpisodeId)
            .WithTitle("Episode 1")
            .Build();

        RuvTvProgram program2Response = new RuvTvProgramBuilder()
            .WithId(Program2RuvId)
            .WithTitle(Program2Name)
            .WithEpisodes([program2Episode])
            .Build();

        _ruv.GetProgramAsync(Program2RuvId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(program2Response));

        _syncQueue.Enqueue(Program1RuvId, Program1Name);
        _syncQueue.Enqueue(Program2RuvId, Program2Name);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert — queue is completely drained; no item stuck in Processing
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — program2's episode was persisted despite program1 throwing
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        episodes.ShouldContain(e => e.RuvId == Program2EpisodeId);
    }

    [Fact]
    public async Task WhenFirstProgramReturnsRequestFailedResult_QueueDrained_SecondProgramEpisodesPersisted()
    {
        // Arrange — AC1/AC4: the actual production timeout path: ApiClient absorbs the timeout
        // into a failure Result<RuvTvProgram> carrying ApiClientErrors.RequestFailed.
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

        // Program1: returns a failure Result — the real production path for a transient timeout
        // after ApiClient absorbs TaskCanceledException into Result.
        _ruv.GetProgramAsync(Program1RuvId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(ApiClientErrors.RequestFailed));

        // Program2: returns success so we can prove it still ran.
        RuvTvEpisode program2Episode = new RuvTvEpisodeBuilder()
            .WithId(Program2EpisodeId)
            .WithTitle("Episode 1")
            .Build();

        RuvTvProgram program2Response = new RuvTvProgramBuilder()
            .WithId(Program2RuvId)
            .WithTitle(Program2Name)
            .WithEpisodes([program2Episode])
            .Build();

        _ruv.GetProgramAsync(Program2RuvId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(program2Response));

        _syncQueue.Enqueue(Program1RuvId, Program1Name);
        _syncQueue.Enqueue(Program2RuvId, Program2Name);

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert — queue is completely drained; no item stuck in Processing
        _syncQueue.Items.ShouldBeEmpty();

        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        List<RuvProgram> programs = await assertContext.Set<RuvProgram>().ToListAsync(cancellationToken);

        // Assert — program2's episode was persisted despite program1 failing
        episodes.ShouldContain(e => e.RuvId == Program2EpisodeId);

        // Assert — program1 was left intact (skipped, not deleted)
        programs.ShouldContain(p => p.RuvId == Program1RuvId);
    }
}
