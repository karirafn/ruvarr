using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class SetManualSeasonsTests
{
    private static readonly int[] SeriesSeasons = [0, 1, 2, 3];

    private static RuvProgram MatchedProgram()
    {
        RuvProgram program = new RuvProgramBuilder().WithName("Bursti I").Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        return program;
    }

    [Fact]
    public void StoresTheSeasonsInAscendingOrder()
    {
        // Arrange
        RuvProgram sut = MatchedProgram();

        // Act
        RuvarrResult result = sut.SetManualSeasons([2, 1, 2], SeriesSeasons);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        sut.ManualSeasons!.Numbers.ShouldBe([1, 2]);
    }

    [Fact]
    public void RejectsAnEmptySet()
    {
        // Arrange
        RuvProgram sut = MatchedProgram();

        // Act
        RuvarrResult result = sut.SetManualSeasons([], SeriesSeasons);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonsEmpty);
        sut.ManualSeasons.ShouldBeNull();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    public void RejectsASeasonThatIsNotARegularSeasonOfTheSeries(int season)
    {
        // Arrange
        RuvProgram sut = MatchedProgram();

        // Act
        RuvarrResult result = sut.SetManualSeasons([1, season], SeriesSeasons);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonNotInSeries);
        sut.ManualSeasons.ShouldBeNull();
    }

    [Fact]
    public void RejectsAProgramWithoutATvdbSeries()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        RuvarrResult result = sut.SetManualSeasons([1], SeriesSeasons);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonsRequireSeries);
        sut.ManualSeasons.ShouldBeNull();
    }

    [Fact]
    public void ErrorsAreKeyedByTheManualSeasonsField()
    {
        // Assert
        ProgramErrors.ManualSeasonsEmpty.Code.ShouldStartWith("Programs.ManualSeasons.");
        ProgramErrors.ManualSeasonNotInSeries.Code.ShouldStartWith("Programs.ManualSeasons.");
        ProgramErrors.ManualSeasonsRequireSeries.Code.ShouldStartWith("Programs.ManualSeasons.");
    }
}
