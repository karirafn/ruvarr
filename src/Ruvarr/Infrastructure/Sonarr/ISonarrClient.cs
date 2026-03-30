
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal interface ISonarrClient
{
    Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SonarrEpisode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder, int? seriesId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default);
    Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default);
    Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default);
}