using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class GetSeriesFailureAbortsMonitoring
{
    private const int RuvProgramId1 = 201;
    private const int RuvProgramId2 = 202;

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public GetSeriesFailureAbortsMonitoring()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MissingEpisode>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private RuvEpisodesSyncJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<RuvEpisodesSyncJob>.Instance,
        _ruv, dbContext, _sonarr, _syncQueue, new DomainEventBroadcaster(), _settingsStore);

    [Fact]
    public async Task WhenGetSeriesAsyncFails_NoSetMonitoredOrMissingCalled_QueueDrained()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using RuvarrDbContext dbContext = CreateDbContext();

        RuvProgram program1 = new RuvProgramBuilder().WithRuvId(RuvProgramId1).WithName("Program One").WithMultipleEpisodes().Build();
        RuvProgram program2 = new RuvProgramBuilder().WithRuvId(RuvProgramId2).WithName("Program Two").WithMultipleEpisodes().Build();

        dbContext.Set<RuvProgram>().Add(program1);
        dbContext.Set<RuvProgram>().Add(program2);
        await dbContext.SaveChangesAsync(cancellationToken);

        bool program1InitialMonitored = program1.IsMonitored;
        bool program2InitialMonitored = program2.IsMonitored;

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(ApiClientErrors.RequestFailed));

        _syncQueue.Enqueue(RuvProgramId1, "Program One");
        _syncQueue.Enqueue(RuvProgramId2, "Program Two");

        RuvEpisodesSyncJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context);

        // Assert — queue fully drained
        _syncQueue.Items.ShouldBeEmpty();

        // Assert — no program flags were written (SetMonitoredStatus / SetHasMissingEpisodes not called)
        List<RuvProgram> programsAfter = await dbContext.Set<RuvProgram>().ToListAsync(cancellationToken);

        RuvProgram p1After = programsAfter.Single(p => p.RuvId == RuvProgramId1);
        RuvProgram p2After = programsAfter.Single(p => p.RuvId == RuvProgramId2);

        p1After.IsMonitored.ShouldBe(program1InitialMonitored);
        p2After.IsMonitored.ShouldBe(program2InitialMonitored);
    }
}
