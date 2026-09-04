using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Testing.Builders;

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

    [Fact]
    public void WhenNotYetCalled_FileNameIsNull()
    {
        // Arrange / Act
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Assert
        sut.FileName.ShouldBeNull();
    }

    [Fact]
    public void WhenCalled_SetsFileNameFromEpisode()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        string expectedFileName = sut.Episode.ToFilename();

        // Act
        sut.MarkDownloading();

        // Assert
        sut.FileName.ShouldBe(expectedFileName);
    }
}
