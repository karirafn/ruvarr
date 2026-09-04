using Ruvarr.Downloads.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class CompletedFileName
{
    [Fact]
    public void IsNull_WhenPending()
    {
        // Arrange / Act
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Assert
        sut.CompletedFileName.ShouldBeNull();
    }

    [Fact]
    public void IsNull_WhenDownloading()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Act
        sut.MarkDownloading();

        // Assert
        sut.CompletedFileName.ShouldBeNull();
    }

    [Fact]
    public void IsNull_WhenComplete()
    {
        // Arrange
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkDownloading();
        sut.MarkDownloaded();

        // Assert
        sut.CompletedFileName.ShouldBeNull();
    }

    [Fact]
    public void IsFileName_WhenFailed()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Assert
        sut.CompletedFileName.ShouldBe(sut.FileName);
    }

    [Fact]
    public void IsFileName_WhenExhausted()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Exhausted().Build();

        // Assert
        sut.CompletedFileName.ShouldBe(sut.FileName);
    }

    [Fact]
    public void IsNull_WhenFailedBeforeMarkDownloading()
    {
        // Arrange — Failed without having gone through MarkDownloading means FileName is null
        DownloadQueueItem sut = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());
        sut.MarkFailed("reason");

        // Assert
        sut.CompletedFileName.ShouldBeNull();
    }
}
