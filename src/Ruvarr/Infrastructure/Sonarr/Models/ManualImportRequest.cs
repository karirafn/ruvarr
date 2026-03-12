namespace Ruvarr.Infrastructure.Sonarr.Models;

internal sealed record class ManualImportRequest(
    string Path,
    int SeriesId,
    IEnumerable<int> EpisodeIds,
    QualityContainer Quality,
    IEnumerable<Language> Languages,
    string ReleaseGroup);