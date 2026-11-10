namespace Ruvarr.Contracts;

public sealed record TvdbEpisodeSummary(
    int TvdbId,
    int SeasonNumber,
    int EpisodeNumber,
    SonarrEpisodeStatus SonarrStatus,
    bool HasIslTranslation,
    Uri? TvdbUrl);