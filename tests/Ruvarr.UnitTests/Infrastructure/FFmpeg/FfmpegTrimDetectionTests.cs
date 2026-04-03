using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg;

public sealed class FfmpegTrimDetectionTests
{
    [Theory]
    // Trim cases — scene change found in window
    [InlineData(2.24, 3.36, 2.84, 0.54, 2.84)]   // Bessie the Rescue Dog S01E01
    [InlineData(0.34, 3.04, 2.48, 1.00, 2.48)]   // Bluey S01E01
    [InlineData(0.26, 2.07, 1.02, 1.00, 1.02)]   // Bob the Builder S02E37
    [InlineData(0.65, 1.83, 1.78, 1.00, 1.78)]   // Ernest and Rebecca S01E36
    [InlineData(1.49, 11.1, 1.45, 1.00, 1.45)]   // Folkid i blokkinni S01E03 — slack case
    [InlineData(0.98, 2.21, 2.13, 1.00, 2.13)]   // JoJo and Gran Gran S01E09
    [InlineData(0.0, 2.01, 1.88, 0.37, 1.88)]    // Magiki S01E45
    [InlineData(2.71, 3.34, 2.78, 0.35, 2.78)]   // Moominvalley S02E05
    [InlineData(0.0, 3.84, 3.60, 1.00, 3.60)]    // Ormhildur The Brave S01E21
    [InlineData(1.97, 2.45, 2.40, 1.00, 2.40)]   // Paw Patrol S08E26
    [InlineData(0.0, 1.32, 1.24, 0.36, 1.24)]    // Zip Zip S01E11
    public void FindTrimPoint_ReturnsSceneChangeTime_WhenSceneChangeInWindow(
        double silenceStart, double silenceEnd, double sceneTime, double sceneScore, double expectedTrimSeconds)
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [(sceneTime, sceneScore)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result.Value.TotalSeconds.ShouldBe(expectedTrimSeconds, 0.001);
    }

    [Theory]
    // Skip cases — silence found but no scene change in window
    [InlineData(0.0, 1.09)]     // Bjornis S03E10
    [InlineData(0.02, 1.36)]    // Corneil & Bernie S01E10
    [InlineData(0.67, 3.61)]    // Daniel Tiger S01E08
    [InlineData(1.58, 2.75)]    // Guess How Much I Love You S01E05
    [InlineData(0.0, 0.48)]     // JoJo and Gran Gran S01E01
    [InlineData(0.82, 2.52)]    // Kate & Mim-Mim S01E17
    [InlineData(0.08, 0.97)]    // Nonni og Manni S01E01
    // Skip cases — silence found but short, no scene change (dash in table)
    [InlineData(0.003, 0.33)]   // Bessie the Rescue Dog S02E01
    [InlineData(0.0, 0.54)]     // Doc McStuffins S02E55 / Moomin S01E33
    [InlineData(0.26, 0.78)]    // DuckTales S01E12
    [InlineData(1.00, 1.24)]    // Lazy Town S01E07
    [InlineData(2.46, 2.76)]    // Simon S01E49
    public void FindTrimPoint_ReturnsNull_WhenNoSceneChangeInWindow(
        double silenceStart, double silenceEnd)
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindTrimPoint_ReturnsNull_WhenSceneChangeOutsideWindow()
    {
        // Arrange
        double silenceStart = 1.0;
        double silenceEnd = 2.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(0.1, 1.0), (5.0, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindTrimPoint_ReturnsFirstSceneChange_WhenMultipleInWindow()
    {
        // Arrange
        double silenceStart = 1.0;
        double silenceEnd = 3.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(1.5, 0.8), (2.5, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result.Value.TotalSeconds.ShouldBe(1.5, 0.001);
    }

    [Fact]
    public void FindTrimPoint_WindowStartClampsToZero()
    {
        // Arrange — silence_start is 0.2, so window start = max(0, 0.2 - 0.5) = 0
        double silenceStart = 0.2;
        double silenceEnd = 1.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(0.15, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result.Value.TotalSeconds.ShouldBe(0.15, 0.001);
    }

    [Fact]
    public void FindTrimPoint_SkipsSceneChangeBelowMinimumTrimPoint()
    {
        // Arrange — scene change at 0.10s is below the 0.15s threshold
        double silenceStart = 0.0;
        double silenceEnd = 2.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(0.10, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindTrimPoint_AcceptsSceneChangeAtExactMinimumTrimPoint()
    {
        // Arrange — scene change at exactly 0.15s meets the threshold
        double silenceStart = 0.0;
        double silenceEnd = 2.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(0.15, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result.Value.TotalSeconds.ShouldBe(0.15, 0.001);
    }

    [Fact]
    public void FindTrimPoint_SkipsSubThresholdSceneChange_ReturnsNextCandidate()
    {
        // Arrange — first scene change at 0.05s is below threshold, second at 1.5s qualifies
        double silenceStart = 0.0;
        double silenceEnd = 2.0;
        List<(double PtsTime, double SceneScore)> sceneChanges = [(0.05, 1.0), (1.5, 0.8)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result.Value.TotalSeconds.ShouldBe(1.5, 0.001);
    }

    [Theory]
    [InlineData(0.0, 1.32, 2.13)]  // Casper and Emma — scene after silence end
    [InlineData(0.0, 2.71, 2.77)]  // Numberblocks — scene after silence end
    public void FindTrimPoint_ReturnsNull_WhenSceneChangeIsAfterSilenceEnd(
        double silenceStart, double silenceEnd, double sceneTime)
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [(sceneTime, 0.5)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(silenceStart, silenceEnd, sceneChanges);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindTrimPoint_ReturnsNull_WhenTooManySceneChangesInWindow()
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [(1.5, 0.8), (2.0, 0.9), (3.0, 0.7), (4.0, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(1.0, 5.0, sceneChanges);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FindTrimPoint_ReturnsTrimPoint_WhenExactlyMaxSceneChangesInWindow()
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [(1.5, 0.8), (2.0, 0.9), (3.0, 0.7)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(1.0, 5.0, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result!.Value.TotalSeconds.ShouldBe(1.5, 0.001);
    }

    [Fact]
    public void FindTrimPoint_IncludesSceneChangeAtExactSilenceEnd()
    {
        // Arrange
        List<(double PtsTime, double SceneScore)> sceneChanges = [(2.0, 1.0)];

        // Act
        TimeSpan? result = FfmpegService.FindTrimPoint(1.0, 2.0, sceneChanges);

        // Assert
        result.ShouldNotBeNull();
        result!.Value.TotalSeconds.ShouldBe(2.0, 0.001);
    }
}
