namespace Ruvarr.Programs.Filters;

internal sealed class ProgramsFilterState
{
    public MatchFilter FilterMatch { get; set; }
    public MonitoredFilter FilterMonitored { get; set; }
    public MissingEpisodesFilter FilterMissingEpisodes { get; set; }
    public PendingLookupFilter FilterPendingLookup { get; set; }
    public ForeignNameFilter FilterForeignName { get; set; }
    public EpisodeMatchFilter FilterEpisodeMatch { get; set; }
    public string FilterChannel { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public double ScrollPositionY { get; set; }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasActiveFilters =>
        FilterMatch != MatchFilter.All ||
        FilterMonitored != MonitoredFilter.All ||
        FilterMissingEpisodes != MissingEpisodesFilter.All ||
        FilterPendingLookup != PendingLookupFilter.All ||
        FilterForeignName != ForeignNameFilter.All ||
        FilterEpisodeMatch != EpisodeMatchFilter.All ||
        !string.IsNullOrEmpty(FilterChannel);

    public void Clear()
    {
        FilterMatch = MatchFilter.All;
        FilterMonitored = MonitoredFilter.All;
        FilterMissingEpisodes = MissingEpisodesFilter.All;
        FilterPendingLookup = PendingLookupFilter.All;
        FilterForeignName = ForeignNameFilter.All;
        FilterEpisodeMatch = EpisodeMatchFilter.All;
        FilterChannel = string.Empty;
        ScrollPositionY = 0;
    }
}
