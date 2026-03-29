using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Events;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Notifiers;

public sealed class DownloadProgressNotifierTests
{
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private DateTimeOffset _currentTime = new(2026, 3, 29, 10, 0, 0, TimeSpan.Zero);

    public DownloadProgressNotifierTests()
    {
        _timeProvider.GetUtcNow().Returns(_ => _currentTime);
    }

    private DownloadProgressNotifier CreateSut() => new(_broadcaster, _timeProvider);

    private void AdvanceTime(TimeSpan duration) => _currentTime = _currentTime.Add(duration);

    [Fact]
    public void IsDownloading_IsFalseByDefault()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();

        // Act & Assert
        sut.IsDownloading.ShouldBeFalse();
    }

    [Fact]
    public void StartDownload_SetsIsDownloadingTrue()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();

        // Act
        sut.StartDownload("Show A", "Episode 1", "S01E01", null);

        // Assert
        sut.IsDownloading.ShouldBeTrue();
        sut.ProgramName.ShouldBe("Show A");
        sut.EpisodeTitle.ShouldBe("Episode 1");
        sut.SeasonEpisodeLabel.ShouldBe("S01E01");
    }

    [Fact]
    public void StartDownload_PublishesProgressUpdatedEvent()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();

        // Act
        sut.StartDownload("Show A", "Episode 1", null, null);

        // Assert
        _broadcaster.Received(1).Publish(Arg.Any<DownloadProgressUpdatedEvent>());
    }

    [Fact]
    public void ReportProgress_UpdatesBytesDownloaded()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, null);

        // Act
        sut.ReportProgress(new FfmpegProgressData(1024 * 1024, TimeSpan.FromSeconds(10)));

        // Assert
        sut.BytesDownloaded.ShouldBe(1024L * 1024);
    }

    [Fact]
    public void CompleteDownload_ResetsState()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", "S01E01", null);
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1)));

        // Act
        sut.CompleteDownload();

        // Assert
        sut.IsDownloading.ShouldBeFalse();
        sut.ProgramName.ShouldBeNull();
        sut.EpisodeTitle.ShouldBeNull();
        sut.SeasonEpisodeLabel.ShouldBeNull();
        sut.BytesDownloaded.ShouldBe(0);
        sut.CompletedLast7Days.ShouldBe(1);
        sut.LastDownloadedAt.ShouldNotBeNull();
    }

    [Fact]
    public void FailDownload_IncrementsFailedCount()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, null);

        // Act
        sut.FailDownload();

        // Assert
        sut.IsDownloading.ShouldBeFalse();
        sut.FailedCount.ShouldBe(1);
    }

    [Fact]
    public void RateBytesPerSecond_IsNull_WhenFewerThanTwoSamples()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, null);
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1)));

        // Act & Assert
        sut.RateBytesPerSecond.ShouldBeNull();
    }

    [Fact]
    public void RateBytesPerSecond_CalculatesRollingAverage()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, null);

        sut.ReportProgress(new FfmpegProgressData(0, TimeSpan.FromSeconds(0)));
        AdvanceTime(TimeSpan.FromSeconds(1));
        sut.ReportProgress(new FfmpegProgressData(1024 * 1024, TimeSpan.FromSeconds(1)));

        // Act
        double? rate = sut.RateBytesPerSecond;

        // Assert
        rate.ShouldNotBeNull();
        rate.Value.ShouldBe(1024.0 * 1024, tolerance: 1.0);
    }

    [Fact]
    public void EstimatedRemaining_IsNull_WhenTotalSizeIsNull()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, totalSize: null);
        sut.ReportProgress(new FfmpegProgressData(0, TimeSpan.FromSeconds(0)));
        AdvanceTime(TimeSpan.FromSeconds(1));
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1)));

        // Act & Assert
        sut.EstimatedRemaining.ShouldBeNull();
    }

    [Fact]
    public void EstimatedRemaining_CalculatesWhenTotalSizeIsKnown()
    {
        // Arrange
        long totalSize = 10 * 1024 * 1024;
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, totalSize: totalSize);

        sut.ReportProgress(new FfmpegProgressData(0, TimeSpan.FromSeconds(0)));
        AdvanceTime(TimeSpan.FromSeconds(1));
        sut.ReportProgress(new FfmpegProgressData(1024 * 1024, TimeSpan.FromSeconds(1)));

        // Act
        TimeSpan? eta = sut.EstimatedRemaining;

        // Assert
        eta.ShouldNotBeNull();
        eta.Value.TotalSeconds.ShouldBe(9.0, tolerance: 0.5);
    }

    [Fact]
    public void SetStatistics_SetsValues()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        DateTimeOffset lastDownloaded = new(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);

        // Act
        sut.SetStatistics(lastDownloaded, completedLast7Days: 5, failedCount: 2);

        // Assert
        sut.LastDownloadedAt.ShouldBe(lastDownloaded);
        sut.CompletedLast7Days.ShouldBe(5);
        sut.FailedCount.ShouldBe(2);
    }

    [Fact]
    public void CompleteDownload_IncrementsSeparateFromSetStatistics()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.SetStatistics(null, completedLast7Days: 3, failedCount: 1);
        sut.StartDownload("Show A", "Episode 1", null, null);

        // Act
        sut.CompleteDownload();

        // Assert
        sut.CompletedLast7Days.ShouldBe(4);
    }

    [Fact]
    public void ReportProgress_SetsTotalSize_WhenEstimatedTotalBytesProvided()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, totalSize: null);

        // Act
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1), EstimatedTotalBytes: 6_000_000));

        // Assert
        sut.TotalSize.ShouldBe(6_000_000L);
    }

    [Fact]
    public void ReportProgress_DoesNotClearTotalSize_WhenSubsequentReportHasNoEstimate()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, totalSize: null);
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1), EstimatedTotalBytes: 6_000_000));

        // Act
        sut.ReportProgress(new FfmpegProgressData(2048, TimeSpan.FromSeconds(2)));

        // Assert
        sut.TotalSize.ShouldBe(6_000_000L);
    }

    [Fact]
    public void ReportProgress_DoesNotOverrideTotalSize_WhenAlreadySetFromStartDownload()
    {
        // Arrange
        DownloadProgressNotifier sut = CreateSut();
        sut.StartDownload("Show A", "Episode 1", null, totalSize: 10_000_000);

        // Act
        sut.ReportProgress(new FfmpegProgressData(1024, TimeSpan.FromSeconds(1), EstimatedTotalBytes: 6_000_000));

        // Assert
        sut.TotalSize.ShouldBe(10_000_000L);
    }
}
