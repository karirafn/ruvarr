namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string? SonarrBaseUrl,
    string? SonarrApiKey,
    string? EpisodeDownloadDirectory,
    string? MovieDownloadDirectory)
{
    public static RuvarrSettings Empty => new(null, null, null, null);
}
