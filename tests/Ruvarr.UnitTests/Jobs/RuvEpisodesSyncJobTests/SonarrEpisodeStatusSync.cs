using Microsoft.EntityFrameworkCore;

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
using Ruvarr.Testing.Logging;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.RuvEpisodesSyncJobTests;

public sealed class SonarrEpisodeStatusSync
{
    private const int RuvProgramId = 301;
    private const string RuvEpisodeId = "ep-301-1";
    private const string EpisodeTitle = "Þáttur 1";
    private const int TvdbSeriesId = 9001;
    private const int SonarrSeriesId = 42;
    private const int TvdbEpisodeId = 5001;

    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IRuvClient _ruv = Substitute.For<IRuvClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ProgramRefreshNotifier _syncQueue = new(TimeProvider.System);
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly RecordingEpisodeMissingHandler _missingHandler = new();
    private readonly RecordingLogger<RuvEpisodesSyncJob> _logger = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public SonarrEpisodeStatusSync()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _serviceProvider.GetService(typeof(IEnumerable<IDomainEventHandler<EpisodeMissingEvent>>))
            .Returns(new IDomainEventHandler<EpisodeMissingEvent>[] { _missingHandler });
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key"));
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>([SonarrSeriesBuilder.Build(SonarrSeriesId, TvdbSeriesId)]));
        _sonarr.GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>())
            .Returns(SonarrEpisode(hasFile: false));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SonarrListsNothingAsMissing());

        RuvTvProgram apiResponse = new RuvTvProgramBuilder()
            .WithId(RuvProgramId)
            .WithEpisodes([new RuvTvEpisodeBuilder().WithId(RuvEpisodeId).WithTitle(EpisodeTitle).Build()])
            .Build();
        _ruv.GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>())
            .Returns((Result<RuvTvProgram>)apiResponse);
    }

    private static Result<IReadOnlyList<SonarrEpisode>> SonarrEpisode(bool hasFile) =>
        new([new SonarrEpisode(Id: 201, SeriesId: SonarrSeriesId, TvdbId: TvdbEpisodeId, SeasonNumber: 1, EpisodeNumber: 1, HasFile: hasFile, Monitored: true)]);

    private static Result<IReadOnlyCollection<MissingEpisode>> SonarrFailure() =>
        new(ApiClientErrors.RequestFailed);

    private static Result<IReadOnlyCollection<MissingEpisode>> SonarrListsEpisodeAsMissing() =>
        new([new MissingEpisodeBuilder().WithTvdbId(TvdbEpisodeId).Build()]);

    private static Result<IReadOnlyCollection<MissingEpisode>> SonarrListsNothingAsMissing() =>
        new(Array.Empty<MissingEpisode>());

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private async Task SeedMatchedEpisodeAsync(SonarrEpisodeStatus status, CancellationToken cancellationToken)
    {
        using RuvarrDbContext seedContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(RuvProgramId).WithMultipleEpisodes().Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(TvdbSeriesId).Build());
        program.TryAddEpisode(
            id: RuvEpisodeId,
            uri: new Uri("http://ruv.is/ep1"),
            title: EpisodeTitle,
            description: string.Empty,
            firstRun: DateTime.UtcNow,
            duration: TimeSpan.FromMinutes(30));
        program.Episodes.Single().Match(TvdbEpisodeId, season: 1, episode: 1, SonarrStatuses.Of(TvdbEpisodeId, status));
        seedContext.Set<RuvProgram>().Add(program);
        await seedContext.SaveChangesAsync(cancellationToken);
        _missingHandler.Events.Clear();
    }

    private async Task RunSyncAsync(CancellationToken cancellationToken)
    {
        _syncQueue.Enqueue(RuvProgramId, "Test Program");
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvEpisodesSyncJob sut = new(
            _logger, _ruv, dbContext, _sonarr, _syncQueue, _broadcaster, _settingsStore);
        await sut.Execute(_context, cancellationToken);
    }

    private async Task<SonarrEpisodeStatus> StoredStatusAsync(CancellationToken cancellationToken)
    {
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .SingleAsync(x => x.RuvId == RuvEpisodeId, cancellationToken);
        return episode.TvdbEpisodes.Single().SonarrStatus;
    }

    [Fact]
    public async Task EpisodeWithFile_StoresDownloaded()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(SonarrEpisodeStatus.NotTracked, cancellationToken);
        _sonarr.GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>()).Returns(SonarrEpisode(hasFile: true));

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await StoredStatusAsync(cancellationToken)).ShouldBe(SonarrEpisodeStatus.Downloaded);
        _missingHandler.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task SeriesNotInSonarr_StoresNotTracked()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(SonarrEpisodeStatus.Downloaded, cancellationToken);
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(Array.Empty<Series>()));

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await StoredStatusAsync(cancellationToken)).ShouldBe(SonarrEpisodeStatus.NotTracked);
        await _sonarr.DidNotReceive().GetEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadsSeriesOnceAndEpisodesOncePerProgram()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(SonarrEpisodeStatus.NotTracked, cancellationToken);

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        await _sonarr.Received(1).GetSeriesAsync(Arg.Any<CancellationToken>());
        await _sonarr.Received(1).GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenLookupFails_StillSyncsProgramAndLogsWarning()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(SonarrEpisodeStatus.Missing, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(SonarrFailure());

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        _syncQueue.Items.ShouldBeEmpty();
        await _ruv.Received(1).GetProgramAsync(RuvProgramId, Arg.Any<CancellationToken>());
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Sonarr", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.Missing)]
    [InlineData(SonarrEpisodeStatus.Downloaded)]
    public async Task WhenLookupFails_LeavesStatusUnchangedAndRaisesNoEpisodeMissingEvent(SonarrEpisodeStatus stored)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(stored, cancellationToken);
        _sonarr.GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<SonarrEpisode>>(ApiClientErrors.RequestFailed));

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await StoredStatusAsync(cancellationToken)).ShouldBe(stored);
        _missingHandler.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task OutageFollowedByHealthySync_KeepsMissingAndRaisesNoEpisodeMissingEvent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(SonarrEpisodeStatus.Missing, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SonarrFailure(), SonarrListsEpisodeAsMissing());

        // Act
        await RunSyncAsync(cancellationToken);
        SonarrEpisodeStatus afterOutage = await StoredStatusAsync(cancellationToken);
        await RunSyncAsync(cancellationToken);
        SonarrEpisodeStatus afterRecovery = await StoredStatusAsync(cancellationToken);

        // Assert
        afterOutage.ShouldBe(SonarrEpisodeStatus.Missing);
        afterRecovery.ShouldBe(SonarrEpisodeStatus.Missing);
        _missingHandler.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.NotTracked)]
    [InlineData(SonarrEpisodeStatus.Downloaded)]
    public async Task TransitionIntoMissing_StoresMissingAndRaisesEpisodeMissingEventOnce(SonarrEpisodeStatus stored)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedMatchedEpisodeAsync(stored, cancellationToken);
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(SonarrListsEpisodeAsMissing());

        // Act
        await RunSyncAsync(cancellationToken);

        // Assert
        (await StoredStatusAsync(cancellationToken)).ShouldBe(SonarrEpisodeStatus.Missing);
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