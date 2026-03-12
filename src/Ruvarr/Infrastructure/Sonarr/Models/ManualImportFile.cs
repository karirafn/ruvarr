namespace Ruvarr.Infrastructure.Sonarr.Models;

internal sealed record class ManualImportFile(
    string Path,
    string RelativePath,
    string Name,
    long Size,
    Series Series,
    int SeasonNumber,
    IReadOnlyList<ManualImportEpisode> Episodes,
    QualityContainer Quality,
    IReadOnlyList<Language> Languages,
    int QualityWeight,
    int CustomFormatScore,
    int IndexerFlags,
    string ReleaseType,
    int Id);