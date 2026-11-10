using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.SonarrWebhook.Commands.ReceiveSonarrWebhook;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrWebhook.ReceiveSonarrWebhookHandlerTests;

public sealed class Handle : IDisposable
{
    private const int TvdbId = 81189;

    private readonly RuvarrDbContext _dbContext;
    private readonly ProgramRefreshNotifier _notifier = new(TimeProvider.System);
    private readonly ISonarrMissingEpisodesCache _missingEpisodesCache = Substitute.For<ISonarrMissingEpisodesCache>();
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly ServiceProvider _serviceProvider = new ServiceCollection().BuildServiceProvider();

    public Handle()
    {
        DbContextOptions<RuvarrDbContext> options = new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new RuvarrDbContext(options, _serviceProvider);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
    }

    private ReceiveSonarrWebhookHandler CreateSut() =>
        new(_dbContext, _notifier, _missingEpisodesCache, _broadcaster, NullLogger<ReceiveSonarrWebhookHandler>.Instance);

    private async Task SeedProgramAsync(int ruvId, string name, int tvdbId)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(ruvId).WithName(name).WithMultipleEpisodes().Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(tvdbId).Build());
        _dbContext.Set<RuvProgram>().Add(program);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    private static ReceiveSonarrWebhookCommand Delivery(string eventType, int? tvdbId = TvdbId) =>
        new(eventType, new SonarrWebhookSeries(tvdbId));

    [Theory]
    [InlineData("Download")]
    [InlineData("EpisodeFileDelete")]
    [InlineData("Rename")]
    public async Task PriorityEnqueuesEveryProgramOfTheSeries(string eventType)
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);
        await SeedProgramAsync(2, "Program B", TvdbId);
        await SeedProgramAsync(3, "Other", TvdbId + 1);
        _notifier.Enqueue(99, "Already queued");

        // Act
        RuvarrResult result = await CreateSut().Handle(Delivery(eventType), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _notifier.Items.Select(i => i.RuvId).ShouldBe([2, 1, 99], ignoreOrder: true);
        _notifier.Items.Take(2).Select(i => i.RuvId).ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public async Task ClearsMissingEpisodesCache_WhenSeriesMatchesAProgram()
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);

        // Act
        await CreateSut().Handle(Delivery("Download"), TestContext.Current.CancellationToken);

        // Assert
        _missingEpisodesCache.Received(1).InvalidateMissingEpisodes();
    }

    [Fact]
    public async Task PublishesProgramRefreshQueueChanged_WhenSeriesMatchesAProgram()
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);

        // Act
        await CreateSut().Handle(Delivery("Download"), TestContext.Current.CancellationToken);

        // Assert
        _broadcaster.Received(1).Publish(Arg.Any<QueueChangedEvent<ProgramRefreshQueueItemSummary>>());
    }

    [Fact]
    public async Task RecordsFollowUpRefresh_WhenProgramSyncIsAlreadyRunning()
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);
        _notifier.Enqueue(1, "Program A");
        IQueueLease lease = _notifier.TryLeaseNext().ShouldNotBeNull();
        _notifier.MarkProcessing(1);

        // Act
        await CreateSut().Handle(Delivery("Download"), TestContext.Current.CancellationToken);
        lease.Dispose();

        // Assert
        ProgramRefreshQueueItemSummary item = _notifier.Items.ShouldHaveSingleItem();
        item.RuvId.ShouldBe(1);
        item.IsProcessing.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Grab")]
    [InlineData("SeriesAdd")]
    [InlineData("Health")]
    public async Task DoesNothing_ForOtherEventTypes(string eventType)
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);

        // Act
        RuvarrResult result = await CreateSut().Handle(Delivery(eventType), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _notifier.Items.ShouldBeEmpty();
        _missingEpisodesCache.DidNotReceive().InvalidateMissingEpisodes();
        _broadcaster.DidNotReceiveWithAnyArgs().Publish<object>(default!);
    }

    [Fact]
    public async Task DoesNothing_WhenSeriesMatchesNoProgram()
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);

        // Act
        RuvarrResult result = await CreateSut().Handle(Delivery("Download", TvdbId + 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _notifier.Items.ShouldBeEmpty();
        _missingEpisodesCache.DidNotReceive().InvalidateMissingEpisodes();
    }

    [Fact]
    public async Task DoesNothing_WhenPayloadHasNoSeries()
    {
        // Act
        RuvarrResult result = await CreateSut().Handle(new ReceiveSonarrWebhookCommand("Download", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _notifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task WritesNoState()
    {
        // Arrange
        await SeedProgramAsync(1, "Program A", TvdbId);

        // Act
        await CreateSut().Handle(Delivery("Download"), TestContext.Current.CancellationToken);

        // Assert
        _dbContext.ChangeTracker.Entries().ShouldBeEmpty();
    }
}
