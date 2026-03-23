namespace Ruvarr.Settings.Commands.SaveSettings;

public sealed record SaveSettingsCommand(
    Uri SonarrBaseAddress,
    string SonarrApiKey,
    string TvdbApiKey,
    string TmdbApiKey,
    string DownloadsRootDirectory,
    string EpisodeDownloadDirectory,
    string MovieDownloadDirectory,
    IReadOnlyList<string> IgnoredChannels);
