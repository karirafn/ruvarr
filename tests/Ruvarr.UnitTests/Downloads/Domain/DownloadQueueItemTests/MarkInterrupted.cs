using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class MarkInterrupted
{
    [Fact]
    public void WhenDownloading_SetsPendingStatus()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.MarkInterrupted();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public void WhenDownloading_DoesNotChangeRetryCount()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.MarkInterrupted();

        // Assert
        sut.RetryCount.ShouldBe(0);
    }

    [Fact]
    public void WhenDownloading_DoesNotChangeNextRetryAt()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.MarkInterrupted();

        // Assert
        sut.NextRetryAt.ShouldBeNull();
    }

    [Fact]
    public void WhenDownloading_DoesNotChangeFailureReason()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.MarkInterrupted();

        // Assert
        sut.FailureReason.ShouldBeNull();
    }

    [Fact]
    public void WhenDownloading_RaisesDownloadInterruptedEvent()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.MarkInterrupted();

        // Assert — MarkDownloading raises DownloadStartedEvent; clear then check MarkInterrupted raised its event
        DownloadInterruptedEvent @event = sut.DomainEvents
            .OfType<DownloadInterruptedEvent>()
            .ShouldHaveSingleItem();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void WhenPending_ThrowsInvalidOperationException()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.MarkInterrupted());
    }

    [Fact]
    public void WhenComplete_ThrowsInvalidOperationException()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();
        sut.MarkDownloaded();

        // Act / Assert
        Should.Throw<InvalidOperationException>(() => sut.MarkInterrupted());
    }
}
