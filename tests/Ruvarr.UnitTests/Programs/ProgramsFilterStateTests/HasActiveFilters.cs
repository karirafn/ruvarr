using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramsFilterStateTests;

public sealed class HasActiveFilters
{
    [Fact]
    public void ReturnsFalseByDefault()
    {
        // Arrange
        ProgramsFilterState sut = new();

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenFilterUnmatchedProgramsIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterUnmatchedPrograms = true };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterUnmatchedEpisodesIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterUnmatchedEpisodes = true };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMissingFromSonarrIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMissingFromSonarr = true };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterPartiallyMatchedIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterPartiallyMatched = true };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMonitoredIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMonitored = true };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterChannelIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterChannel = "RUV" };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenOnlySearchTextIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { SearchText = "test" };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeFalse();
    }
}
