using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg;

public sealed class FfmpegStderrParserTests
{
    [Fact]
    public void ParsesTypicalProgressLine()
    {
        // Arrange
        const string line = "frame=  120 fps= 30 q=-1.0 size=    5120kB time=00:02:30.50 bitrate= 279.2kbits/s speed=1.5x";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldNotBeNull();
        result.BytesDownloaded.ShouldBe(5120L * 1024);
        result.Time.ShouldBe(new TimeSpan(0, 0, 2, 30, 500));
    }

    [Fact]
    public void ParsesCompactProgressLine()
    {
        // Arrange
        const string line = "size=   1024kB time=00:00:10.00 bitrate= 838.9kbits/s";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldNotBeNull();
        result.BytesDownloaded.ShouldBe(1024L * 1024);
        result.Time.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ReturnsNull_WhenLineHasNoSizeField()
    {
        // Arrange
        const string line = "time=00:01:00.00 bitrate= 128kbits/s";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_WhenLineHasNoTimeField()
    {
        // Arrange
        const string line = "size=   1024kB bitrate= 128kbits/s";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_ForEmptyLine()
    {
        // Arrange & Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_ForNullLine()
    {
        // Arrange & Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_ForUnrelatedLogLine()
    {
        // Arrange
        const string line = "Input #0, hls, from 'https://example.com/stream.m3u8':";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ParsesZeroSize()
    {
        // Arrange
        const string line = "size=       0kB time=00:00:00.00 bitrate=N/A speed=N/A";

        // Act
        FfmpegProgressData? result = FfmpegStderrParser.TryParse(line);

        // Assert
        result.ShouldNotBeNull();
        result.BytesDownloaded.ShouldBe(0L);
        result.Time.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TryParseDuration_ReturnsEstimatedBytes_ForTypicalDurationLine()
    {
        // Arrange
        const string line = "  Duration: 00:01:00.00, start: 0.000000, bitrate: 800 kb/s";

        // Act
        long? result = FfmpegStderrParser.TryParseDuration(line);

        // Assert — 60 seconds * 800 kbps * 1000 / 8 = 6_000_000 bytes
        result.ShouldBe(6_000_000L);
    }

    [Fact]
    public void TryParseDuration_ReturnsNull_ForProgressLine()
    {
        // Arrange
        const string line = "size=   1024kB time=00:00:10.00 bitrate= 838.9kbits/s";

        // Act
        long? result = FfmpegStderrParser.TryParseDuration(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseDuration_ReturnsNull_ForNullInput()
    {
        // Arrange & Act
        long? result = FfmpegStderrParser.TryParseDuration(null!);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseDuration_ReturnsNull_ForEmptyInput()
    {
        // Arrange & Act
        long? result = FfmpegStderrParser.TryParseDuration("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseDuration_ReturnsNull_WhenBitrateIsNotAvailable()
    {
        // Arrange
        const string line = "  Duration: 00:01:00.00, start: 0.000000, bitrate: N/A";

        // Act
        long? result = FfmpegStderrParser.TryParseDuration(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseDuration_HandlesLongerDuration()
    {
        // Arrange
        const string line = "  Duration: 01:30:00.50, start: 0.000000, bitrate: 1200 kb/s";

        // Act
        long? result = FfmpegStderrParser.TryParseDuration(line);

        // Assert — (1*3600 + 30*60 + 0 + 0.50) seconds = 5400.5s; 5400.5 * 1200 * 1000 / 8 = 810_075_000
        result.ShouldBe(810_075_000L);
    }
}
