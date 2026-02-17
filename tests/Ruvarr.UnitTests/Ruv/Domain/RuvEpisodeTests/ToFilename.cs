using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Ruv.Domain.RuvEpisodeTests;

public sealed class ToFilename
{
    [Fact]
    public void ReturnsFilename_WithProgramName_WhenTvdbSeriesIsNotMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Terrible Title")
            .Build();
        sut.Match(1234, 2, 3);

        // Act
        string result = sut.ToFilename();

        // Assert
        result.ShouldBe("Awesome.Show.S02E03.Terrible.Title-RUV.mp4");
    }

    [Fact]
    public void ReturnsFilename_WithTvdbSeriesName_WhenTvdbSeriesIsMatched()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvProgram program = new RuvProgramBuilder().Build();
        program.MatchTvdb(series);
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Terrible Title")
            .Build();
        sut.Match(1234, 2, 3);

        // Act
        string result = sut.ToFilename();

        // Assert
        result.ShouldBe("Awesome.Show.S02E03.Terrible.Title-RUV.mp4");
    }
}