using Ruvarr.Programs.Filters;

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
    public void ReturnsTrueWhenFilterMatchIsMatched()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMatch = MatchFilter.Matched };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMatchIsUnmatched()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMatch = MatchFilter.Unmatched };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMonitoredIsMonitored()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMonitored = MonitoredFilter.Monitored };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMonitoredIsNotMonitored()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMonitored = MonitoredFilter.NotMonitored };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMissingEpisodesIsMissingEpisodes()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMissingEpisodes = MissingEpisodesFilter.MissingEpisodes };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterMissingEpisodesIsNotMissingEpisodes()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterMissingEpisodes = MissingEpisodesFilter.NotMissingEpisodes };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterPendingLookupIsPendingLookup()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterPendingLookup = PendingLookupFilter.PendingLookup };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterPendingLookupIsNotPendingLookup()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterPendingLookup = PendingLookupFilter.NotPendingLookup };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterForeignNameIsHasForeignName()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterForeignName = ForeignNameFilter.HasForeignName };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterForeignNameIsNoForeignName()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterForeignName = ForeignNameFilter.NoForeignName };

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
    public void ReturnsTrueWhenFilterEpisodeMatchIsFullyMatched()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterEpisodeMatch = EpisodeMatchFilter.FullyMatched };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterEpisodeMatchIsPartiallyMatched()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterEpisodeMatch = EpisodeMatchFilter.PartiallyMatched };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenFilterEpisodeMatchIsNoneMatched()
    {
        // Arrange
        ProgramsFilterState sut = new() { FilterEpisodeMatch = EpisodeMatchFilter.NoneMatched };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsTrueWhenSearchTextIsSet()
    {
        // Arrange
        ProgramsFilterState sut = new() { SearchText = "test" };

        // Act
        bool result = sut.HasActiveFilters;

        // Assert
        result.ShouldBeTrue();
    }
}
