namespace Ruvarr.Infrastructure.Sonarr.Models;

public sealed record class ManualImportEpisode(
    int SeriesId,
    int TvdbId,
    int EpisodeFileId,
    int SeasonNumber,
    int EpisodeNumber,
    string Title,
    DateOnly Airdate,
    DateTime AirDateUtc,
    DateTime LastSearchTime,
    int Runtime,
    bool HasFile,
    bool Monitored,
    bool UnverifiedSceneNumbering,
    int Id);