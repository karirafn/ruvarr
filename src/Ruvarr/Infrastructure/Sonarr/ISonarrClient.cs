
using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal interface ISonarrClient
{
    Task<Result<IReadOnlyList<Series>>> GetSeriesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SonarrEpisode>>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ManualImportFile>>> GetManualImportsAsync(string folder, int? seriesId = null, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyCollection<MissingEpisode>>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default);
    Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RootFolder>>> GetRootFoldersAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<QualityProfile>>> GetQualityProfilesAsync(CancellationToken cancellationToken = default);
    Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default);
}
