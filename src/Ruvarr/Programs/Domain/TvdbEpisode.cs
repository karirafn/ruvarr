using Ruvarr.Contracts;

namespace Ruvarr.Programs.Domain;

internal sealed class TvdbEpisode
{
    private TvdbEpisode()
    {
    }

    public required int TvdbId { get; init; }

    public required int SeasonNumber { get; init; }

    public required int EpisodeNumber { get; init; }

    public SonarrEpisodeStatus SonarrStatus { get; internal set; }

    public bool HasIslTranslation { get; internal set; }

    internal static TvdbEpisode Create(int tvdbId, int seasonNumber, int episodeNumber, bool hasIslTranslation = false)
    {
        return new TvdbEpisode
        {
            TvdbId = tvdbId,
            SeasonNumber = seasonNumber,
            EpisodeNumber = episodeNumber,
            HasIslTranslation = hasIslTranslation,
        };
    }
}