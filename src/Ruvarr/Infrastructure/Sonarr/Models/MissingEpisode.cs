namespace Ruvarr.Infrastructure.Sonarr.Models;

public sealed record class MissingEpisode(
    int Id,
    int SeriesId,
    int TvdbId,
    int EpisodeFileId,
    int SeasonNumber,
    int Episodenumber,
    int AbsoluteEpisodeNumber,
    string Title,
    string Overview,
    string FinaleType,
    DateOnly AirDate,
    DateTime AirDateUtc,
    int Runtime,
    bool HasFile,
    bool Monitored,
    bool UnverifiedSceneNumbering,
    DateTime LastSearchTime);