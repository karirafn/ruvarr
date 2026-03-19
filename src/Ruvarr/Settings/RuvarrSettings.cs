namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string SonarrBaseAddress = "",
    string SonarrApiKey = "",
    string DownloadsRootDirectory = "",
    string EpisodeDownloadDirectory = "",
    string MovieDownloadDirectory = "",
    string FfmpegPath = "")
{
    public IReadOnlyList<string> IgnoredChannels { get; init; } = [];

    public static RuvarrSettings Empty => new();
}
