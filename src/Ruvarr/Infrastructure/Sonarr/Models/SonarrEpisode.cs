namespace Ruvarr.Infrastructure.Sonarr.Models;

internal sealed record class SonarrEpisode(int Id, int SeriesId, int TvdbId, int SeasonNumber, int EpisodeNumber);
