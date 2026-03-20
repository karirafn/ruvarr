using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class ToString
{
    [Fact]
    public void ReturnsProgramNameAndTitle_WhenEpisodeIsNotMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Krakkar")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Fyrsti dagurinn")
            .Build();

        // Act
        string? result = sut.ToString();

        // Assert
        result.ShouldBe("Krakkar - Fyrsti dagurinn");
    }

    [Fact]
    public void ReturnsProgramNameSeasonEpisodeAndTitle_WhenEpisodeIsMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Krakkar")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Fyrsti dagurinn")
            .Build();
        sut.Match(tvdbId: 1234, season: 1, episode: 3, isMissing: false);

        // Act
        string? result = sut.ToString();

        // Assert
        result.ShouldBe("Krakkar S01E03 - Fyrsti dagurinn");
    }

    [Fact]
    public void FormatsHighSeasonAndEpisodeNumbers_WithD2Formatting()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Krakkar")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Fyrsti dagurinn")
            .Build();
        sut.Match(tvdbId: 5678, season: 12, episode: 145, isMissing: false);

        // Act
        string? result = sut.ToString();

        // Assert
        result.ShouldBe("Krakkar S12E145 - Fyrsti dagurinn");
    }
}
