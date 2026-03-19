namespace Ruvarr.Settings.Commands.SaveSettings;

public sealed record SaveSettingsCommand(
    Uri? SonarrBaseUrl,
    string? SonarrApiKey,
    string? EpisodeDownloadDirectory,
    string? MovieDownloadDirectory);
