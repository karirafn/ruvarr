using Ruvarr.Abstractions;

namespace Ruvarr.Settings;

public static class SettingsErrors
{
    public const string InvalidSonarrBaseAddressCode = "Settings.InvalidSonarrBaseAddress";
    public const string DownloadsRootDirectoryNotFoundCode = "Settings.DownloadsRootDirectoryNotFound";
    public const string EpisodeDownloadDirectoryNotFoundCode = "Settings.EpisodeDownloadDirectoryNotFound";
    public const string MovieDownloadDirectoryNotFoundCode = "Settings.MovieDownloadDirectoryNotFound";

    public static readonly RuvarrError InvalidSonarrBaseAddress = new(
        InvalidSonarrBaseAddressCode,
        "Sonarr base address must be a valid absolute URI.");

    public static readonly RuvarrError DownloadsRootDirectoryNotFound = new(
        DownloadsRootDirectoryNotFoundCode,
        "Downloads root directory does not exist.");

    public static readonly RuvarrError EpisodeDownloadDirectoryNotFound = new(
        EpisodeDownloadDirectoryNotFoundCode,
        "Episode download directory does not exist.");

    public static readonly RuvarrError MovieDownloadDirectoryNotFound = new(
        MovieDownloadDirectoryNotFoundCode,
        "Movie download directory does not exist.");
}
