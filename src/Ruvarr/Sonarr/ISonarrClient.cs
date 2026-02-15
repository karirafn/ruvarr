
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Sonarr;

internal interface ISonarrClient
{
    Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder);
    Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue);
    Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files);
}