using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class Retry
{
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public Retry()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private static async Task<DownloadQueueItem> SaveAndBackdateAsync(
        RuvarrDbContext dbContext,
        DownloadQueueItem item,
        CancellationToken cancellationToken)
    {
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Simulate time passing: backdate next_retry_at so the item is now due
        dbContext.Entry(item).Property(x => x.NextRetryAt).CurrentValue = DateTime.UtcNow.AddHours(-1);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await dbContext.Set<DownloadQueueItem>().FirstAsync(cancellationToken);
    }

    // RequeueForRetry — guard tests

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenStatusIsPending()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenStatusIsExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenFailedButNotDue()
    {
        // Arrange — Failed item with NextRetryAt in the future
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public async Task RequeueForRetry_SetsPendingStatus_WhenItemIsDue()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem sut = await SaveAndBackdateAsync(
            dbContext,
            new DownloadQueueItemBuilder().Failed("reason").Build(),
            TestContext.Current.CancellationToken);

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public async Task RequeueForRetry_PreservesRetryCount()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem sut = await SaveAndBackdateAsync(
            dbContext,
            new DownloadQueueItemBuilder().Failed("reason").Build(),
            TestContext.Current.CancellationToken);
        int expectedRetryCount = sut.RetryCount;

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.RetryCount.ShouldBe(expectedRetryCount);
    }

    [Fact]
    public async Task RequeueForRetry_ClearsNextRetryAt()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem sut = await SaveAndBackdateAsync(
            dbContext,
            new DownloadQueueItemBuilder().Failed("reason").Build(),
            TestContext.Current.CancellationToken);

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public async Task RequeueForRetry_RaisesDownloadRetryScheduledEvent()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem sut = await SaveAndBackdateAsync(
            dbContext,
            new DownloadQueueItemBuilder().Failed("reason").Build(),
            TestContext.Current.CancellationToken);
        sut.ClearDomainEvents();

        // Act
        sut.RequeueForRetry();

        // Assert
        DownloadRetryScheduledEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadRetryScheduledEvent>();
        @event.Item.ShouldBe(sut);
    }

    // RetryNow tests

    [Fact]
    public void RetryNow_SetsPendingStatus_FromFailed()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act
        sut.RetryNow();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public void RetryNow_SetsPendingStatus_FromExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();

        // Act
        sut.RetryNow();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public void RetryNow_ClearsNextRetryAt()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act
        sut.RetryNow();

        // Assert
        sut.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public void RetryNow_ClearsFailureReason()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("original reason").Build();

        // Act
        sut.RetryNow();

        // Assert
        sut.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void RetryNow_PreservesRetryCount()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();
        int expectedRetryCount = sut.RetryCount;

        // Act
        sut.RetryNow();

        // Assert
        sut.RetryCount.ShouldBe(expectedRetryCount);
    }

    [Fact]
    public void RetryNow_RaisesDownloadRetryScheduledEvent()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();
        sut.ClearDomainEvents();

        // Act
        sut.RetryNow();

        // Assert
        DownloadRetryScheduledEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadRetryScheduledEvent>();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void RetryNow_ThrowsInvalidOperation_WhenPending()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }

    [Fact]
    public void RetryNow_ThrowsInvalidOperation_WhenDownloading()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }

    [Fact]
    public void RetryNow_ThrowsInvalidOperation_WhenComplete()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();
        sut.MarkDownloaded();

        // Act
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }
}
