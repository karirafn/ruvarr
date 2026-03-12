using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Ruv.Domain.RuvEpisodeTests;

public sealed class ToFilename
{
    [Fact]
    public void ReturnsFilename_WithProgramName_and_EpisodeTitle_WhenSeriesIsNotMatched_and_EpisodeIsNotMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithName("Awesome Show")
            .Build();
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle("Terrible Title")
            .Build();

        // Act
        string result = sut.ToFilename();

        // Assert
        result.ShouldBe("Awesome.Show.Terrible.Title-RUV.mp4");
    }

    [Theory]
    [InlineData("Awesome Show", "Terrible Title", "Awesome.Show.Terrible.Title-RUV.mp4")]
    [InlineData("Gettu betur í 40 ár", "Þáttur 1 af 2", "Gettu.betur.í.40.ár.Þáttur.1.af.2-RUV.mp4")]
    public void ReturnsFilename_WithSeriesName_and_EpisodeTitle_WhenSeriesIsMatched_and_EpisodeIsNotMatched(string seriesName, string episodeName, string expected)
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder()
            .WithName(seriesName)
            .Build();
        RuvProgram program = new RuvProgramBuilder().Build();
        program.MatchTvdb(series);
        RuvEpisode sut = new RuvEpisodeBuilder()
            .WithProgram(program)
            .WithTitle(episodeName)
            .Build();

        // Act
        string result = sut.ToFilename();

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public void ReturnsFilename_WithSeriesName_and_SeriesAndEpisodeNumber_WhenSeriesIsMatched_and_EpisodeIsMatched()
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
        sut.Match(1234, 2, 3);

        // Act
        string result = sut.ToFilename();

        // Assert
        result.ShouldBe("Awesome.Show.S02E03-RUV.mp4");
    }
}