using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class ToFilePath
{
    [Fact]
    public void UsesProgramNameForDirectory_WhenSeriesIsNotMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Test Episode")
            .Build();

        // Act
        string result = sut.ToFilePath("/root", "episodes", fileAlreadyExists: false);

        // Assert
        result.ShouldBe(Path.Join("/root", "episodes", "Awesome Show", "Awesome.Show.Test.Episode-RUV.mp4"));
    }

    [Fact]
    public void UsesSeriesNameForDirectory_WhenSeriesIsMatched()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Kveikur")
            .Build();
        program.MatchTvdb(series);
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Test Episode")
            .Build();

        // Act
        string result = sut.ToFilePath("/root", "episodes", fileAlreadyExists: false);

        // Assert
        result.ShouldBe(Path.Join("/root", "episodes", "Awesome Show", "Awesome.Show.Test.Episode-RUV.mp4"));
    }

    [Fact]
    public void UsesSeasonEpisodeNumber_WhenEpisodeIsMatched()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvProgram program = new RuvProgramBuilder().Build();
        program.MatchTvdb(series);
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .Build();
        sut.Match(1234, 2, 3, isMissing: false);

        // Act
        string result = sut.ToFilePath("/root", "episodes", fileAlreadyExists: false);

        // Assert
        result.ShouldBe(Path.Join("/root", "episodes", "Awesome Show", "Awesome.Show.S02E03-RUV.mp4"));
    }

    [Fact]
    public void AppliesXSuffix_WhenFileAlreadyExists()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Test Episode")
            .Build();

        // Act
        string result = sut.ToFilePath("/root", "episodes", fileAlreadyExists: true);

        // Assert
        result.ShouldBe(Path.Join("/root", "episodes", "Awesome Show", "Awesome.Show.Test.Episode-RUV.X.mp4"));
    }
}
