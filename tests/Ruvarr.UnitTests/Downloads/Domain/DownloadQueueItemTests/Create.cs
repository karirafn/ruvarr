using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class Create
{
    [Fact]
    public void SetsEpisode()
    {
        // Arrange
        RuvEpisode episode = new RuvEpisodeBuilder().Build();

        // Act
        DownloadQueueItem result = DownloadQueueItem.Create(episode);

        // Assert
        result.Episode.ShouldBe(episode);
    }

    [Fact]
    public void SetsCreatedToUtcNow()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        DownloadQueueItem result = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Assert
        result.Created.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void SetsPendingStatus()
    {
        // Arrange / Act
        DownloadQueueItem result = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Assert
        result.Status.ShouldBe(DownloadQueueStatus.Pending);
    }

    [Fact]
    public void SetsDownloadedToNull()
    {
        // Arrange / Act
        DownloadQueueItem result = DownloadQueueItem.Create(new RuvEpisodeBuilder().Build());

        // Assert
        result.Downloaded.ShouldBeNull();
    }
}
