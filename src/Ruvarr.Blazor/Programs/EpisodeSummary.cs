namespace Ruvarr.Blazor.Programs;

internal sealed record EpisodeSummary(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    string? SeriesName,
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    int? TvdbId,
    int? SeasonNumber,
    int? EpisodeNumber,
    DateTime FirstRun);
