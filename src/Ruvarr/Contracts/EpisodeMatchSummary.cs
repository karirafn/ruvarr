namespace Ruvarr.Contracts;

public sealed record EpisodeMatchSummary(
    int TvdbId,
    int SeasonNumber,
    int EpisodeNumber,
    bool IsMissing);
