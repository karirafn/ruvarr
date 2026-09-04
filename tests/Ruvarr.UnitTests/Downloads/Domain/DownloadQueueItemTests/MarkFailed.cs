using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class MarkFailed
{
    [Fact]
    public void SetsFailedStatus()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkFailed("test reason");

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Failed);
    }

    [Fact]
    public void RaisesDownloadFailedEvent()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkFailed("test reason");

        // Assert
        DownloadFailedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadFailedEvent>();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void SetsFailureReason()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkFailed("FFmpeg download failed");

        // Assert
        sut.FailureReason.ShouldBe("FFmpeg download failed");
    }

    [Fact]
    public void IncrementsRetryCount()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkFailed("test reason");

        // Assert
        sut.RetryCount.ShouldBe(1);
    }

    [Fact]
    public void SetsNextRetryAt_WhenWithinRetryBudget()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        DateTime before = DateTime.UtcNow;

        // Act
        sut.MarkFailed("test reason");

        // Assert — first failure: RetryCount=1 → 1-hour rung
        sut.NextRetryAt.ShouldNotBeNull();
        sut.NextRetryAt.Value.ShouldBeInRange(before.AddHours(1), DateTime.UtcNow.AddHours(1));
    }

    [Fact]
    public void AccumulatesRetryCount_AcrossMultipleFailures()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkFailed("reason 1");
        sut.MarkFailed("reason 2");

        // Assert
        sut.RetryCount.ShouldBe(2);
    }

    [Fact]
    public void SetsExhaustedStatus_WhenRetryBudgetExceeded()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        for (int i = 0; i < RetrySchedule.MaxRetries; i++)
        {
            sut.MarkFailed("reason");
        }

        // Act
        sut.MarkFailed("final failure");

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Exhausted);
    }

    [Fact]
    public void SetsNextRetryAtToNull_WhenExhausted()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        for (int i = 0; i <= RetrySchedule.MaxRetries; i++)
        {
            sut.MarkFailed("reason");
        }

        // Assert
        sut.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public void StillRaisesDownloadFailedEvent_WhenExhausted()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        for (int i = 0; i < RetrySchedule.MaxRetries; i++)
        {
            sut.MarkFailed("reason");
        }
        sut.ClearDomainEvents();

        // Act
        sut.MarkFailed("final failure");

        // Assert
        sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadFailedEvent>();
    }
}
