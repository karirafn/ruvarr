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

public sealed class EpisodeRemoval
{
    private const int RuvProgramId = 42;
    private const string KeptEpisodeId = "ep-1";
    private const string RemovedEpisodeId = "ep-2";

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public EpisodeRemoval()
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

        RuvTvEpisode keptApiEpisode = new RuvTvEpisodeBuilder()
            .WithId(KeptEpisodeId)
            .WithTitle("Episode 1")
            .Build();

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithEpisodes([keptApiEpisode])
            .Build();

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvEpisode> remaining = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);
        remaining.Count.ShouldBe(1);
        remaining[0].RuvId.ShouldBe(KeptEpisodeId);
    }

    [Fact]
    public async Task DeletesProgramAndEpisodes_WhenGetProgramAsyncReturnsNotFound()
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

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns(new Result<RuvTvProgram>(ApiClientErrors.NotFound));

        _syncQueue.Enqueue(RuvProgramId, "Test Program");

        using RuvarrDbContext actContext = CreateDbContext();
        RuvEpisodesSyncJob sut = CreateJob(actContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        List<RuvProgram> programs = await assertContext.Set<RuvProgram>().ToListAsync(cancellationToken);
        List<RuvEpisode> episodes = await assertContext.Set<RuvEpisode>().ToListAsync(cancellationToken);

        programs.ShouldBeEmpty();
        episodes.ShouldBeEmpty();
    }
}
