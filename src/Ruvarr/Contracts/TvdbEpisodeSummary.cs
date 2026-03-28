namespace Ruvarr.Contracts;

public sealed record TvdbEpisodeSummary(
    int TvdbId,
    int SeasonNumber,
    int EpisodeNumber,
    bool IsMissing,
    bool HasIslTranslation,
    Uri? TvdbUrl);
