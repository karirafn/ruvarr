using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class MarkDownloading
{
    [Fact]
    public void SetsDownloadingStatus()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkDownloading();

        // Assert
        sut.Status.ShouldBe(DownloadQueueStatus.Downloading);
    }
}
