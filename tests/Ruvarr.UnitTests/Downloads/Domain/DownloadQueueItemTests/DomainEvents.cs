using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class DomainEvents
{
    [Fact]
    public void MarkDownloading_RaisesDownloadStartedEvent()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkDownloading();

        // Assert
        DownloadStartedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadStartedEvent>();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void MarkDownloaded_RaisesDownloadCompletedEvent()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkDownloaded();

        // Assert
        DownloadCompletedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadCompletedEvent>();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void MarkFailed_RaisesDownloadFailedEvent()
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
    public void ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();

        // Act
        sut.ClearDomainEvents();

        // Assert
        sut.DomainEvents.ShouldBeEmpty();
    }
}
