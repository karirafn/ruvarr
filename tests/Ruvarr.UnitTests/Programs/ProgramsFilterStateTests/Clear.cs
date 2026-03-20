using Ruvarr.Programs.Filters;

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
            FilterMatch = MatchFilter.Unmatched,
            FilterMonitored = MonitoredFilter.Monitored,
            FilterMissingEpisodes = MissingEpisodesFilter.MissingEpisodes,
            FilterPendingLookup = PendingLookupFilter.PendingLookup,
            FilterForeignName = ForeignNameFilter.HasForeignName,
            FilterChannel = "RUV",
            FilterEpisodeMatch = EpisodeMatchFilter.FullyMatched
        };

        // Act
        sut.Clear();

        // Assert
        sut.HasActiveFilters.ShouldBeFalse();
        sut.FilterMatch.ShouldBe(MatchFilter.All);
        sut.FilterMonitored.ShouldBe(MonitoredFilter.All);
        sut.FilterMissingEpisodes.ShouldBe(MissingEpisodesFilter.All);
        sut.FilterPendingLookup.ShouldBe(PendingLookupFilter.All);
        sut.FilterForeignName.ShouldBe(ForeignNameFilter.All);
        sut.FilterChannel.ShouldBeEmpty();
        sut.FilterEpisodeMatch.ShouldBe(EpisodeMatchFilter.All);
    }

    [Fact]
    public void DoesNotResetSearchText()
    {
        // Arrange
        ProgramsFilterState sut = new() { SearchText = "test query" };

        // Act
        sut.Clear();

        // Assert
        sut.SearchText.ShouldBe("test query");
    }

    [Fact]
    public void ResetsScrollPositionYToZero()
    {
        // Arrange
        ProgramsFilterState sut = new() { ScrollPositionY = 500.0 };

        // Act
        sut.Clear();

        // Assert
        sut.ScrollPositionY.ShouldBe(0);
    }
}
