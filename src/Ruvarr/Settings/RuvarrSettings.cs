namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string SonarrBaseAddress = "",
    string SonarrApiKey = "",
    string TvdbApiKey = "",
    string TmdbApiKey = "",
    string EpisodeDownloadDirectory = "tv",
    string MovieDownloadDirectory = "movies")
{
    public const string DownloadsRoot = "/downloads";

    public IReadOnlyList<string> IgnoredChannels { get; init; } = [];

    public bool IsTvdbConfigured => !string.IsNullOrWhiteSpace(TvdbApiKey);

    public bool IsTmdbConfigured => !string.IsNullOrWhiteSpace(TmdbApiKey);

    public bool IsSonarrConfigured => !string.IsNullOrWhiteSpace(SonarrBaseAddress) && !string.IsNullOrWhiteSpace(SonarrApiKey);

    public bool IsReady => IsTvdbConfigured && IsTmdbConfigured && IsSonarrConfigured;

    public string ResolvedEpisodeDownloadDirectory => Path.Join(DownloadsRoot, EpisodeDownloadDirectory);

    public string ResolvedMovieDownloadDirectory => Path.Join(DownloadsRoot, MovieDownloadDirectory);

    public static RuvarrSettings Empty => new();
}
