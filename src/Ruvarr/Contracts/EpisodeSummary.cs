namespace Ruvarr.Contracts;

public sealed record EpisodeSummary(
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    IReadOnlyList<EpisodeMatchSummary> TvdbMatches,
    DateTime FirstRun,
    Uri? RuvUrl,
    TimeSpan Duration);