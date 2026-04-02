using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg;

public sealed class FfmpegSceneParserTests
{
    [Fact]
    public void TryParsePtsTime_ParsesTypicalLine()
    {
        // Arrange
        const string line = "pts_time:2.475";

        // Act
        double? result = FfmpegSceneParser.TryParsePtsTime(line);

        // Assert
        result.ShouldBe(2.475);
    }

    [Fact]
    public void TryParsePtsTime_ParsesZero()
    {
        // Arrange
        const string line = "pts_time:0.000000";

        // Act
        double? result = FfmpegSceneParser.TryParsePtsTime(line);

        // Assert
        result.ShouldBe(0.0);
    }

    [Fact]
    public void TryParsePtsTime_ReturnsNull_ForSceneScoreLine()
    {
        // Arrange
        const string line = "lavfi.scene_score=1.000000";

        // Act
        double? result = FfmpegSceneParser.TryParsePtsTime(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParsePtsTime_ReturnsNull_ForEmptyString()
    {
        // Arrange & Act
        double? result = FfmpegSceneParser.TryParsePtsTime("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParsePtsTime_ReturnsNull_ForUnrelatedLine()
    {
        // Arrange
        const string line = "frame=  120 fps= 30 q=-1.0 size=    5120kB";

        // Act
        double? result = FfmpegSceneParser.TryParsePtsTime(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSceneScore_ParsesTypicalLine()
    {
        // Arrange
        const string line = "lavfi.scene_score=1.000000";

        // Act
        double? result = FfmpegSceneParser.TryParseSceneScore(line);

        // Assert
        result.ShouldBe(1.0);
    }

    [Fact]
    public void TryParseSceneScore_ParsesLowScore()
    {
        // Arrange
        const string line = "lavfi.scene_score=0.350000";

        // Act
        double? result = FfmpegSceneParser.TryParseSceneScore(line);

        // Assert
        result.ShouldBe(0.35);
    }

    [Fact]
    public void TryParseSceneScore_ReturnsNull_ForPtsTimeLine()
    {
        // Arrange
        const string line = "pts_time:2.475";

        // Act
        double? result = FfmpegSceneParser.TryParseSceneScore(line);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSceneScore_ReturnsNull_ForEmptyString()
    {
        // Arrange & Act
        double? result = FfmpegSceneParser.TryParseSceneScore("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParseSceneScore_ReturnsNull_ForUnrelatedLine()
    {
        // Arrange
        const string line = "Input #0, hls, from 'https://example.com/stream.m3u8':";

        // Act
        double? result = FfmpegSceneParser.TryParseSceneScore(line);

        // Assert
        result.ShouldBeNull();
    }
}
