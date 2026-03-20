using Shouldly;

using ProgramsPage = Ruvarr.Programs.Programs;

namespace Ruvarr.UnitTests.Programs.ProgramsPageTests;

public sealed class GetMatchButtonLabel
{
    [Fact]
    public void ReturnsMatchWhenTvdbSeriesIdIsNullAndIsNotMovieMatch()
    {
        // Arrange
        int? tvdbSeriesId = null;
        bool isMovieMatch = false;

        // Act
        string result = ProgramsPage.GetMatchButtonLabel(tvdbSeriesId, isMovieMatch);

        // Assert
        result.ShouldBe("Match");
    }

    [Fact]
    public void ReturnsFixMatchWhenTvdbSeriesIdHasValue()
    {
        // Arrange
        int? tvdbSeriesId = 12345;
        bool isMovieMatch = false;

        // Act
        string result = ProgramsPage.GetMatchButtonLabel(tvdbSeriesId, isMovieMatch);

        // Assert
        result.ShouldBe("Fix Match");
    }

    [Fact]
    public void ReturnsFixMatchWhenIsMovieMatch()
    {
        // Arrange
        int? tvdbSeriesId = null;
        bool isMovieMatch = true;

        // Act
        string result = ProgramsPage.GetMatchButtonLabel(tvdbSeriesId, isMovieMatch);

        // Assert
        result.ShouldBe("Fix Match");
    }

    [Fact]
    public void ReturnsFixMatchWhenBothTvdbSeriesIdHasValueAndIsMovieMatch()
    {
        // Arrange
        int? tvdbSeriesId = 12345;
        bool isMovieMatch = true;

        // Act
        string result = ProgramsPage.GetMatchButtonLabel(tvdbSeriesId, isMovieMatch);

        // Assert
        result.ShouldBe("Fix Match");
    }
}
