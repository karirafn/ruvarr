using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal static class SonarrEpisodeStatusLookup
{
    /// <summary>
    /// Looks up the Sonarr episode status of every episode of a TVDB series, keyed by TVDB episode id.
    /// Episodes absent from the result are not tracked by Sonarr.
    /// </summary>
    public static async Task<Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>>> GetEpisodeStatusesAsync(
        this ISonarrClient sonarr,
        int tvdbSeriesId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sonarr);

        Result<IReadOnlyList<Series>> seriesResult = await sonarr.GetSeriesAsync(cancellationToken);

        if (seriesResult.IsFailure)
        {
            return seriesResult.Error;
        }

        return await sonarr.GetEpisodeStatusesAsync(tvdbSeriesId, seriesResult.FromResult(), cancellationToken);
    }

    // Takes Sonarr's series list from a caller that looks up many series, so the list is read once.
    public static async Task<Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>>> GetEpisodeStatusesAsync(
        this ISonarrClient sonarr,
        int tvdbSeriesId,
        IReadOnlyList<Series> sonarrSeries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sonarr);
        ArgumentNullException.ThrowIfNull(sonarrSeries);

        if (sonarrSeries.FirstOrDefault(s => s.TvdbId == tvdbSeriesId) is not Series series)
        {
            return new Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>>(new Dictionary<int, SonarrEpisodeStatus>());
        }

        Result<IReadOnlyList<SonarrEpisode>> episodesResult = await sonarr.GetEpisodesAsync(series.Id, cancellationToken);

        if (episodesResult.IsFailure)
        {
            return episodesResult.Error;
        }

        Result<IReadOnlyCollection<MissingEpisode>> missingResult =
            await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);

        if (missingResult.IsFailure)
        {
            return missingResult.Error;
        }

        HashSet<int> missingTvdbIds = [.. missingResult.FromResult().Select(e => e.TvdbId)];

        Dictionary<int, SonarrEpisodeStatus> statuses = [];
        foreach (SonarrEpisode episode in episodesResult.FromResult())
        {
            statuses[episode.TvdbId] = StatusOf(episode, missingTvdbIds);
        }

        return new Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>>(statuses);
    }

    // hasFile wins over the (cached) missing list, so a stale list cannot show a downloaded episode as Missing.
    private static SonarrEpisodeStatus StatusOf(SonarrEpisode episode, HashSet<int> missingTvdbIds)
    {
        if (episode.HasFile)
        {
            return SonarrEpisodeStatus.Downloaded;
        }

        return missingTvdbIds.Contains(episode.TvdbId)
            ? SonarrEpisodeStatus.Missing
            : SonarrEpisodeStatus.NotTracked;
    }
}