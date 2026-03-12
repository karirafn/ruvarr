using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class MarkDownloaded
{
    [Fact]
    public void SetsCompleteStatus()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkDownloaded();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Complete);
    }

    [Fact]
    public void SetsDownloadedToUtcNow()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        DateTime before = DateTime.UtcNow;

        // Act
        sut.MarkDownloaded();

        // Assert
        sut.Downloaded.ShouldNotBeNull();
        sut.Downloaded.Value.ShouldBeInRange(before, DateTime.UtcNow);
    }
}
