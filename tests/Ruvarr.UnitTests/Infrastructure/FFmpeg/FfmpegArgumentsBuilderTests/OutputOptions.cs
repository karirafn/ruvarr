using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg.FfmpegArgumentsBuilderTests;

public sealed class OutputOptions
{
    [Fact]
    public void WithInput_FilePath_ProducesMinusI_FollowedByPath()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithInput("/path/to/file.mp4");

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-i");
        idx.ShouldBeGreaterThanOrEqualTo(0);
        result[idx + 1].ShouldBe("/path/to/file.mp4");
    }

    [Fact]
    public void WithAudioFilter_ProducesMinusAf_FollowedByFilter()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithAudioFilter("silencedetect=n=-40dB:d=0.2");

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-af");
        idx.ShouldBeGreaterThanOrEqualTo(0);
        result[idx + 1].ShouldBe("silencedetect=n=-40dB:d=0.2");
    }

    [Fact]
    public void WithVideoFilter_ProducesMinusVf_FollowedByFilter()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithVideoFilter("select=gt(scene\\,0.2),metadata=print:file=-");

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-vf");
        idx.ShouldBeGreaterThanOrEqualTo(0);
        result[idx + 1].ShouldBe("select=gt(scene\\,0.2),metadata=print:file=-");
    }

    [Fact]
    public void WithDuration_ProducesMinusT_FollowedBySeconds()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithDuration(15.0);

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-t");
        idx.ShouldBeGreaterThanOrEqualTo(0);
        result[idx + 1].ShouldBe("15");
    }

    [Fact]
    public void WithDuration_FormatsDecimalWithInvariantCulture()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithDuration(12.1);

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-t");
        result[idx + 1].ShouldBe("12.1");
    }

    [Fact]
    public void WithNoAudio_ProducesMinusAn()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithNoAudio();

        // Act
        List<string> result = sut.Build();

        // Assert
        result.ShouldContain("-an");
    }

    [Fact]
    public void WithNullOutput_ProducesMinusFNull_AndDashOutput()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithNullOutput();

        // Act
        List<string> result = sut.Build();

        // Assert
        int fIdx = result.IndexOf("-f");
        fIdx.ShouldBeGreaterThanOrEqualTo(0);
        result[fIdx + 1].ShouldBe("null");
        result[^1].ShouldBe("-");
    }

    [Fact]
    public void WithSeekOutput_AppearsAfterArguments_BeforeOutput()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithInput("/path/to/file.mp4")
            .WithCodec("copy")
            .WithSeekOutput(2.84)
            .WithOutput("/path/to/output.mp4");

        // Act
        List<string> result = sut.Build();

        // Assert
        int ssIdx = result.IndexOf("-ss");
        ssIdx.ShouldBeGreaterThanOrEqualTo(0);
        result[ssIdx + 1].ShouldBe("2.84");

        // -ss should appear after the arguments (-i, -c) but before the output path
        int inputIdx = result.IndexOf("-i");
        int outputIdx = result.IndexOf("/path/to/output.mp4");
        ssIdx.ShouldBeGreaterThan(inputIdx);
        ssIdx.ShouldBeLessThan(outputIdx);
    }

    [Fact]
    public void WithSeekOutput_FormatsDecimalWithInvariantCulture()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithSeekOutput(1.45);

        // Act
        List<string> result = sut.Build();

        // Assert
        int ssIdx = result.IndexOf("-ss");
        result[ssIdx + 1].ShouldBe("1.45");
    }

    [Fact]
    public void Build_OrderIsCorrect_ArgumentsThenOutputOptionsThenOutput()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithInput("/path/to/input.mp4")
            .WithCodec("copy")
            .WithSeekOutput(2.84)
            .OverwriteOutputFiles()
            .WithOutput("/path/to/output.mp4");

        // Act
        List<string> result = sut.Build();

        // Assert
        // Arguments: -i, /path/to/input.mp4, -c, copy, -y
        // Output options: -ss, 2.84
        // Output: /path/to/output.mp4
        int codecIdx = result.IndexOf("-c");
        int ssIdx = result.IndexOf("-ss");
        int outputIdx = result.IndexOf("/path/to/output.mp4");

        codecIdx.ShouldBeLessThan(ssIdx);
        ssIdx.ShouldBeLessThan(outputIdx);
        result[^1].ShouldBe("/path/to/output.mp4");
    }
}
