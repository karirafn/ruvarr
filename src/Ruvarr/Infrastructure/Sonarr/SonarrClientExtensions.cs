using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal static class SonarrClientExtensions
{
    public static async Task<HashSet<int>> GetMissingTvdbIdsAsync(
        this ISonarrClient sonarr,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);
        return [.. missingEpisodes.Select(x => x.TvdbId)];
    }
}
