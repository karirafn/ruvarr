using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Domain.DownloadQueueItemTests;

public sealed class IsSuperseded
{
    private static RuvEpisode DownloadedEpisode() =>
        new RuvEpisodeBuilder().BuildMatchedWithStatus(SonarrEpisodeStatus.Downloaded);

    [Fact]
    public void IsTrue_WhenFailedAndEpisodeDownloaded()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Failed("reason").Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeTrue();
    }

    [Fact]
    public void IsTrue_WhenExhaustedAndEpisodeDownloaded()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Exhausted().Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.Missing)]
    [InlineData(SonarrEpisodeStatus.NotTracked)]
    public void IsFalse_WhenFailedAndEpisodeNotDownloaded(SonarrEpisodeStatus status)
    {
        // Arrange
        RuvEpisode episode = new RuvEpisodeBuilder().BuildMatchedWithStatus(status);
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(episode).Failed("reason").Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenFailedAndEpisodeUnmatched()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().Failed("reason").Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenOnlySomeLinkedEpisodesDownloaded()
    {
        // Arrange
        RuvEpisode episode = new RuvEpisodeBuilder().Build();
        episode.MatchMultiple(
            [TvdbEpisode.Create(1, 1, 1), TvdbEpisode.Create(2, 1, 2)],
            SonarrEpisodeStatuses.From(new Dictionary<int, SonarrEpisodeStatus>
            {
                [1] = SonarrEpisodeStatus.Downloaded,
                [2] = SonarrEpisodeStatus.Missing,
            }),
            MatchOrigin.Automatic);
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(episode).Failed("reason").Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenPendingAndEpisodeDownloaded()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenDownloadingAndEpisodeDownloaded()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Build();
        sut.MarkDownloading();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }

    [Fact]
    public void IsFalse_WhenCompleteAndEpisodeDownloaded()
    {
        // Arrange
        DownloadQueueItem sut = new DownloadQueueItemBuilder().WithEpisode(DownloadedEpisode()).Downloaded().Build();

        // Act & Assert
        sut.IsSuperseded.ShouldBeFalse();
    }
}
