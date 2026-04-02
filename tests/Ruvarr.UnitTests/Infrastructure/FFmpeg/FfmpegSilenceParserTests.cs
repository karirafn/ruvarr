using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg;

public sealed class FfmpegSilenceParserTests
{
    [Fact]
    public void TryParseSilenceStart_ParsesTypicalLine()
    {
        // Arrange
        const string line = "[silencedetect @ 0x5f] silence_start: 1.234";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart(line);

        // Assert
        result.ShouldBe(1.234);
    }

    [Fact]
    public void TryParseSilenceStart_ParsesZero()
    {
        // Arrange
        const string line = "[silencedetect @ 0x5f] silence_start: 0";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart(line);

        // Assert
        result.ShouldBe(0.0);
    }

    [Fact]
    public void TryParseSilenceStart_ParsesLineWithoutPrefix()
    {
        // Arrange
        const string line = "silence_start: 2.46";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart(line);

        // Assert
        result.ShouldBe(2.46);
    }

    [Fact]
    public void TryParseSilenceStart_ReturnsNull_ForSilenceEndLine()
    {
        // Arrange
        const string line = "[silencedetect @ 0x5f] silence_end: 2.345 | silence_duration: 1.111";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSilenceStart_ReturnsNull_ForEmptyString()
    {
        // Arrange & Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSilenceStart_ReturnsNull_ForUnrelatedLine()
    {
        // Arrange
        const string line = "frame=  120 fps= 30 q=-1.0 size=    5120kB";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceStart(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSilenceEnd_ParsesTypicalLine()
    {
        // Arrange
        const string line = "[silencedetect @ 0x5f] silence_end: 2.345 | silence_duration: 1.111";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceEnd(line);

        // Assert
        result.ShouldBe(2.345);
    }

    [Fact]
    public void TryParseSilenceEnd_ParsesLineWithoutPrefix()
    {
        // Arrange
        const string line = "silence_end: 3.04 | silence_duration: 2.7";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceEnd(line);

        // Assert
        result.ShouldBe(3.04);
    }

    [Fact]
    public void TryParseSilenceEnd_ReturnsNull_ForSilenceStartLine()
    {
        // Arrange
        const string line = "[silencedetect @ 0x5f] silence_start: 1.234";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceEnd(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSilenceEnd_ReturnsNull_ForEmptyString()
    {
        // Arrange & Act
        double? result = FfmpegSilenceParser.TryParseSilenceEnd("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSilenceEnd_ReturnsNull_ForUnrelatedLine()
    {
        // Arrange
        const string line = "Input #0, hls, from 'https://example.com/stream.m3u8':";

        // Act
        double? result = FfmpegSilenceParser.TryParseSilenceEnd(line);

        // Assert
        result.ShouldBeNull();
    }
}
