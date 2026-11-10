using Ruvarr.Contracts;

namespace Ruvarr.Programs.Domain;

/// <summary>
/// The outcome of a Sonarr episode status lookup, as handed to a match: either the statuses Sonarr
/// reported, or <see cref="Unavailable"/> when the lookup failed.
/// </summary>
internal sealed class SonarrEpisodeStatuses
{
    private readonly IReadOnlyDictionary<int, SonarrEpisodeStatus>? _statuses;

    private SonarrEpisodeStatuses(IReadOnlyDictionary<int, SonarrEpisodeStatus>? statuses)
    {
        _statuses = statuses;
    }

    public static SonarrEpisodeStatuses Unavailable { get; } = new(null);

    public static SonarrEpisodeStatuses From(IReadOnlyDictionary<int, SonarrEpisodeStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return new(statuses);
    }

    // A failed lookup must not overwrite what an earlier healthy read stored.
    internal SonarrEpisodeStatus StatusFor(int tvdbId, SonarrEpisodeStatus storedStatus) =>
        _statuses is null
            ? storedStatus
            : _statuses.GetValueOrDefault(tvdbId, SonarrEpisodeStatus.NotTracked);
}