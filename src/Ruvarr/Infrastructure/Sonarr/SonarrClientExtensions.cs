using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal static class SonarrClientExtensions
{
    public static async Task<Result<HashSet<int>>> GetMissingTvdbIdsAsync(
        this ISonarrClient sonarr,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyCollection<MissingEpisode>> missingEpisodes =
            await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);

        return missingEpisodes.Match<Result<HashSet<int>>>(
            episodes => new HashSet<int>(episodes.Select(x => x.TvdbId)),
            error => error);
    }
}
