using Ruvarr.Downloads.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class CanRetry
{
    [Fact]
    public void IsTrue_WhenFailed()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act & Assert
        sut.CanRetry.ShouldBeTrue();
    }

    [Fact]
    public void IsTrue_WhenExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();

        // Act & Assert
        sut.CanRetry.ShouldBeTrue();
    }

    [Fact]
    public void IsFalse_WhenPending()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Build();

        // Act & Assert
        sut.CanRetry.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenDownloading()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Build();
        sut.MarkDownloading();

        // Act & Assert
        sut.CanRetry.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenComplete()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Downloaded().Build();

        // Act & Assert
        sut.CanRetry.ShouldBeFalse();
    }
}
