namespace Ruvarr.Settings.Commands.SaveSettings;

public sealed record SaveSettingsCommand(
    Uri SonarrBaseAddress,
    string SonarrApiKey,
    string DownloadsRootDirectory,
    string EpisodeDownloadDirectory,
    string MovieDownloadDirectory,
    IReadOnlyList<string> IgnoredChannels,
    string FfmpegPath);
