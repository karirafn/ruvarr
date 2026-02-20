
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Sonarr;

internal interface ISonarrClient
{
    Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default);
    Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default);
}