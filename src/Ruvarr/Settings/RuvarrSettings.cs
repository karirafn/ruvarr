namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string SonarrBaseAddress = "",
    string SonarrApiKey = "",
    string DownloadsRootDirectory = "/downloads",
    string EpisodeDownloadDirectory = "/downloads/tv",
    string MovieDownloadDirectory = "/downloads/movies")
{
    public IReadOnlyList<string> IgnoredChannels { get; init; } = [];

    public static RuvarrSettings Empty => new();
}
