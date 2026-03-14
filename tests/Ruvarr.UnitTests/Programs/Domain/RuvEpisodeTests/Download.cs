using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class Download
{
    [Fact]
    public void SetsDownloadQueueItem()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Download();

        // Assert
        sut.DownloadQueueItem.ShouldNotBeNull();
    }

    [Fact]
    public void DownloadQueueItemReferencesEpisode()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Download();

        // Assert
        sut.DownloadQueueItem!.Episode.ShouldBe(sut);
    }
}
