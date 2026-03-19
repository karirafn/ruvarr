using Ruvarr.Abstractions;

namespace Ruvarr.Settings;

public static class SettingsErrors
{
    public const string InvalidSonarrBaseUrlCode = "Settings.InvalidSonarrBaseUrl";
    public const string EpisodeDownloadDirectoryNotFoundCode = "Settings.EpisodeDownloadDirectoryNotFound";
    public const string MovieDownloadDirectoryNotFoundCode = "Settings.MovieDownloadDirectoryNotFound";

    public static readonly RuvarrError InvalidSonarrBaseUrl = new(
        InvalidSonarrBaseUrlCode,
        "Sonarr base URL must be a valid absolute URI.");

    public static readonly RuvarrError EpisodeDownloadDirectoryNotFound = new(
        EpisodeDownloadDirectoryNotFoundCode,
        "Episode download directory does not exist.");

    public static readonly RuvarrError MovieDownloadDirectoryNotFound = new(
        MovieDownloadDirectoryNotFoundCode,
        "Movie download directory does not exist.");
}
