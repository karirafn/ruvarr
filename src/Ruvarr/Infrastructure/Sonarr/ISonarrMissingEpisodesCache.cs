namespace Ruvarr.Infrastructure.Sonarr;

internal interface ISonarrMissingEpisodesCache
{
    void InvalidateMissingEpisodes();
}
