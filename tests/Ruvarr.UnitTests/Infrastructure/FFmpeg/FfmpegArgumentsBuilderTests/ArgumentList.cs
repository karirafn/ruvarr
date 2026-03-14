using Ruvarr.Infrastructure.FFmpeg;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.FFmpeg.FfmpegArgumentsBuilderTests;

public sealed class ArgumentList
{
    [Fact]
    public void WithMetadata_ValueContainingQuotes_DoesNotSplitIntoMultipleArguments()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithMetadata("title", @"She said ""hello""");

        // Act
        List<string> result = sut.Build();

        // Assert
        result.ShouldContain("-metadata");
        result.ShouldContain(@"title=She said ""hello""");
    }

    [Fact]
    public void WithMetadata_ValueContainingSpaces_IsPassedAsASingleEntry()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithMetadata("title", "My Show S01E02");

        // Act
        List<string> result = sut.Build();

        // Assert
        int metadataIndex = result.IndexOf("-metadata");
        metadataIndex.ShouldBeGreaterThanOrEqualTo(0);
        result[metadataIndex + 1].ShouldBe("title=My Show S01E02");
    }

    [Fact]
    public void WithInput_ProducesMinusI_FollowedByUri()
    {
        // Arrange
        Uri input = new Uri("https://example.com/stream.m3u8");
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithInput(input);

        // Act
        List<string> result = sut.Build();

        // Assert
        int idx = result.IndexOf("-i");
        idx.ShouldBeGreaterThanOrEqualTo(0);
        result[idx + 1].ShouldBe(input.ToString());
    }

    [Fact]
    public void WithOutput_ProducesOutputPathAsLastEntry()
    {
        // Arrange
        FfmpegArgumentsBuilder sut = new FfmpegArgumentsBuilder()
            .WithOutput("/path/to/my file.mp4");

        // Act
        List<string> result = sut.Build();

        // Assert
        result[result.Count - 1].ShouldBe("/path/to/my file.mp4");
    }
}
