namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string SonarrBaseAddress = "",
    string SonarrApiKey = "",
    string TvdbApiKey = "",
    string TmdbApiKey = "",
    string DownloadsRootDirectory = "/downloads",
    string EpisodeDownloadDirectory = "/downloads/tv",
    string MovieDownloadDirectory = "/downloads/movies")
{
    public IReadOnlyList<string> IgnoredChannels { get; init; } = [];

    public bool IsTvdbConfigured => !string.IsNullOrWhiteSpace(TvdbApiKey);

    public bool IsTmdbConfigured => !string.IsNullOrWhiteSpace(TmdbApiKey);

    public bool IsSonarrConfigured => !string.IsNullOrWhiteSpace(SonarrBaseAddress) && !string.IsNullOrWhiteSpace(SonarrApiKey);

    public bool IsDownloadsConfigured => !string.IsNullOrWhiteSpace(DownloadsRootDirectory);

    public bool IsReady => IsTvdbConfigured && IsTmdbConfigured && IsSonarrConfigured && IsDownloadsConfigured;

    public static RuvarrSettings Empty => new();
}
