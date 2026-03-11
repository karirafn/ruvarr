namespace Ruvarr.Blazor.Programs;

internal sealed record EpisodeSummary(
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    int? TvdbId,
    int? SeasonNumber,
    int? EpisodeNumber,
    DateTime FirstRun);
