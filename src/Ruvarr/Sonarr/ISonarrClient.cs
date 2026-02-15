
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Sonarr;

internal interface ISonarrClient
{
    Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue);
}