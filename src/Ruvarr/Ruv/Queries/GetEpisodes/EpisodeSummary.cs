namespace Ruvarr.Ruv.Queries.GetEpisodes;

public sealed record class EpisodeSummary(
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    int? TvdbId,
    int? SeasonNumber,
    int? EpisodeNumber,
    DateTime FirstRun);