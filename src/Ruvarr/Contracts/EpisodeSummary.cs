namespace Ruvarr.Contracts;

public sealed record EpisodeSummary(
    string EpisodeTitle,
    string EpisodeRuvId,
    string EpisodeDescription,
    IReadOnlyList<TvdbEpisodeSummary> TvdbMatches,
    DateTime FirstRun,
    Uri? RuvUrl,
    TimeSpan Duration);