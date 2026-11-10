using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class Dismiss
{
    [Fact]
    public void RaisesDownloadDismissedEvent_WhenFailed()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();
        sut.ClearDomainEvents();

        // Act
        sut.Dismiss();

        // Assert
        DownloadDismissedEvent @event = sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadDismissedEvent>();
        @event.Item.ShouldBe(sut);
    }

    [Fact]
    public void RaisesDownloadDismissedEvent_WhenExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();
        sut.ClearDomainEvents();

        // Act
        sut.Dismiss();

        // Assert
        sut.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DownloadDismissedEvent>();
    }

    [Fact]
    public void ThrowsInvalidOperation_WhenPending()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Dismiss());
    }

    [Fact]
    public void ThrowsInvalidOperation_WhenDownloading()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Build();
        sut.MarkDownloading();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Dismiss());
    }

    [Fact]
    public void ThrowsInvalidOperation_WhenComplete()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Downloaded().Build();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => sut.Dismiss());
    }
}
