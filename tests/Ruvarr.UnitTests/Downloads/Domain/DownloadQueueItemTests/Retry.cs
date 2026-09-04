using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class Retry
{
    // RequeueForRetry — guard tests

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenStatusIsPending()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenStatusIsExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public void RequeueForRetry_ThrowsInvalidOperation_WhenFailedButNotDue()
    {
        // Arrange — Failed item with NextRetryAt in the future
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RequeueForRetry());
    }

    [Fact]
    public void RequeueForRetry_SetsPendingStatus_WhenItemIsDue()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().FailedAndDue("reason").Build();

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public void RequeueForRetry_PreservesRetryCount()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().FailedAndDue("reason").Build();
        int expectedRetryCount = sut.RetryCount;

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.RetryCount.ShouldBe(expectedRetryCount);
    }

    [Fact]
    public void RequeueForRetry_ClearsNextRetryAt()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().FailedAndDue("reason").Build();

        // Act
        sut.RequeueForRetry();

        // Assert
        sut.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public void RequeueForRetry_RaisesDownloadRetryScheduledEvent()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().FailedAndDue("reason").Build();
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

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }

    [Fact]
    public void RetryNow_ThrowsInvalidOperation_WhenDownloading()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }

    [Fact]
    public void RetryNow_ThrowsInvalidOperation_WhenComplete()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();
        sut.MarkDownloaded();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.RetryNow());
    }
}
