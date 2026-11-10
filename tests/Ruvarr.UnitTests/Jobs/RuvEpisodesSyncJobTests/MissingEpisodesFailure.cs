using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class MissingEpisodesFailure
{
    private const int RuvProgramId = 301;
    private const string RuvEpisodeId = "ep-301-1";
    private const string EpisodeTitle = "Þáttur 1";
    private const int TvdbEpisodeId = 5001;

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly RecordingEpisodeMissingHandler _missingHandler = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public MissingEpisodesFailure()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _serviceProvider.GetService(typeof(IEnumerable<IDomainEventHandler<EpisodeMissingEvent>>))
            .Returns(new IDomainEventHandler<EpisodeMissingEvent>[] { _missingHandler });
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(Array.Empty<Series>()));

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithEpisodes([new RuvTvEpisodeBuilder().WithId(RuvEpisodeId).WithTitle(EpisodeTitle).Build()])
            .Build();
        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);
    }

    private static Result<IReadOnlyCollection<MissingEpisode>> SonarrFailure() =>
        new(ApiClientErrors.RequestFailed);

    private static Result<IReadOnlyCollection<MissingEpisode>> SonarrListsEpisodeAsMissing() =>
        new([new MissingEpisodeBuilder().WithTvdbId(TvdbEpisodeId).Build()]);

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private async Task SeedMatchedEpisodeAsync(bool isMissing, CancellationToken cancellationToken)
    {
        using RuvarrDbContext seedContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(RuvProgramId).WithMultipleEpisodes().Build();
        program.TryAddEpisode(
            id: RuvEpisodeId,
            uri: new Uri("http://ruv.is/ep1"),
            title: EpisodeTitle,
            description: string.Empty,
            firstRun: DateTime.UtcNow,
            duration: TimeSpan.FromMinutes(30));
        program.Episodes.Single().Match(TvdbEpisodeId, season: 1, episode: 1, isMissing: isMissing);
        seedContext.Set<RuvProgram>().Add(program);
        await seedContext.SaveChangesAsync(cancellationToken);
        _missingHandler.Events.Clear();
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        _syncQueue.Enqueue(RuvProgramId, "Test Program");
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvEpisodesSyncJob sut = new(
            NullLogger<RuvEpisodesSyncJob>.Instance,
            _ruv, dbContext, _sonarr, _syncQueue, _broadcaster, _settingsStore);
        await sut.Execute(_context, cancellationToken);
    }

    private async Task<bool> IsMissingAsync(CancellationToken cancellationToken)
    {
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .SingleAsync(x => x.RuvId == RuvEpisodeId, cancellationToken);
        return episode.TvdbEpisodes.Single().IsMissing;
    }

    [Fact]
    public async Task WhenMissingEpisodesFails_ReleasesLeasesAndPublishesQueueChanged()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(isMissing: true, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(SonarrFailure());

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        _syncQueue.Items.ShouldBeEmpty();
        _broadcaster.Received().Publish(Arg.Any<QueueChangedEvent<ProgramRefreshQueueItemSummary>>());
        await _ruv.DidNotReceive().GetProgramAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenMissingEpisodesFails_LeavesIsMissingUnchangedAndRaisesNoEpisodeMissingEvent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(isMissing: true, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(SonarrFailure());

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await IsMissingAsync(cancellationToken)).ShouldBeTrue();
        _missingHandler.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task OutageFollowedByHealthySync_KeepsIsMissingTrueAndRaisesNoEpisodeMissingEvent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(isMissing: true, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SonarrFailure(), SonarrListsEpisodeAsMissing());

        // Act
        await RunSyncAsync(cancellationToken);
        bool isMissingAfterOutage = await IsMissingAsync(cancellationToken);
        await RunSyncAsync(cancellationToken);
        bool isMissingAfterRecovery = await IsMissingAsync(cancellationToken);

        // Assert
        isMissingAfterOutage.ShouldBeTrue();
        isMissingAfterRecovery.ShouldBeTrue();
        _missingHandler.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task EpisodeMatchedDuringOutage_HealthySyncSetsIsMissingAndRaisesEpisodeMissingEventOnce()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(isMissing: false, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SonarrListsEpisodeAsMissing());

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await IsMissingAsync(cancellationToken)).ShouldBeTrue();
        _missingHandler.Events.ShouldHaveSingleItem().Episode.RuvId.ShouldBe(RuvEpisodeId);
    }

    private sealed class RecordingEpisodeMissingHandler : IDomainEventHandler<EpisodeMissingEvent>
    {
        public List<EpisodeMissingEvent> Events { get; } = [];

        public Task Handle(EpisodeMissingEvent @event, CancellationToken cancellationToken)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
