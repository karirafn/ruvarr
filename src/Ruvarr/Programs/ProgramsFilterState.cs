namespace Ruvarr.Programs;

internal sealed class ProgramsFilterState
{
    public bool FilterUnmatchedPrograms { get; set; }
    public bool FilterUnmatchedEpisodes { get; set; }
    public bool FilterMissingFromSonarr { get; set; }
    public bool FilterPartiallyMatched { get; set; }
    public bool FilterMonitored { get; set; }
    public string FilterChannel { get; set; } = string.Empty;
    public string SearchText { get; set; } = string.Empty;
    public double ScrollPositionY { get; set; }

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    public bool HasActiveFilters =>
        FilterUnmatchedPrograms ||
        FilterUnmatchedEpisodes ||
        FilterMissingFromSonarr ||
        FilterPartiallyMatched ||
        FilterMonitored ||
        !string.IsNullOrEmpty(FilterChannel);

    public void Clear()
    {
        FilterUnmatchedPrograms = false;
        FilterUnmatchedEpisodes = false;
        FilterMissingFromSonarr = false;
        FilterPartiallyMatched = false;
        FilterMonitored = false;
        FilterChannel = string.Empty;
        ScrollPositionY = 0;
    }
}
