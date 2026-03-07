namespace Ruvarr.Ruv.Queries.GetEpisodes;

public sealed record class EpisodeSummary(
    string ProgramName,
    string? SeriesName,
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    int? TvdbId,
    int? SeasonNumber,
    int? EpisodeNumber);