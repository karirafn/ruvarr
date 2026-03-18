using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramsFilterStateTests;

public sealed class Clear
{
    [Fact]
    public void ResetsAllFiltersToDefaults()
    {
        // Arrange
        ProgramsFilterState sut = new()
        {
            FilterUnmatchedPrograms = true,
            FilterUnmatchedEpisodes = true,
            FilterMissingFromSonarr = true,
            FilterPartiallyMatched = true,
            FilterMonitored = true,
            FilterChannel = "RUV"
        };

        // Act
        sut.Clear();

        // Assert
        sut.HasActiveFilters.ShouldBeFalse();
        sut.FilterUnmatchedPrograms.ShouldBeFalse();
        sut.FilterUnmatchedEpisodes.ShouldBeFalse();
        sut.FilterMissingFromSonarr.ShouldBeFalse();
        sut.FilterPartiallyMatched.ShouldBeFalse();
        sut.FilterMonitored.ShouldBeFalse();
        sut.FilterChannel.ShouldBeEmpty();
    }
}
