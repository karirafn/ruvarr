namespace Ruvarr.Programs.Components;

/// <summary>
/// The season selector's staged edits: Automatic or a non-empty set of seasons, never neither, so the selection
/// a user can save is always valid.
/// </summary>
internal sealed class SeasonSelection
{
    private readonly SortedSet<int> _saved;
    private readonly SortedSet<int> _seasons;

    public SeasonSelection(IEnumerable<int> savedManualSeasons)
    {
        ArgumentNullException.ThrowIfNull(savedManualSeasons);

        _saved = [.. savedManualSeasons];
        _seasons = [.. _saved];
    }

    public bool IsAutomatic => _seasons.Count == 0;

    public IReadOnlyCollection<int> Seasons => _seasons;

    public bool IsUnchanged => _seasons.SetEquals(_saved);

    public bool IsChecked(int season) => _seasons.Contains(season);

    /// <returns>What a screen reader should hear about rows changed as a side effect, if any.</returns>
    public string? ToggleAutomatic()
    {
        if (IsAutomatic)
        {
            // Unchecking Automatic with no season chosen would leave nothing selected.
            return null;
        }

        _seasons.Clear();
        return "Seasons cleared";
    }

    /// <returns>What a screen reader should hear about rows changed as a side effect, if any.</returns>
    public string? ToggleSeason(int season)
    {
        if (_seasons.Remove(season))
        {
            return IsAutomatic ? "Automatic turned on" : null;
        }

        bool wasAutomatic = IsAutomatic;
        _seasons.Add(season);
        return wasAutomatic ? "Automatic turned off" : null;
    }
}
