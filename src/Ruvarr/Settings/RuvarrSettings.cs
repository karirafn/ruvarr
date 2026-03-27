namespace Ruvarr.Settings;

internal sealed record RuvarrSettings(
    string SonarrBaseAddress = "",
    string SonarrApiKey = "",
    string TvdbApiKey = "",
    string TmdbApiKey = "",
    string EpisodeDownloadDirectory = "tv",
    string MovieDownloadDirectory = "movies")
{
    public static string DownloadsRoot { get; internal set; } = "/downloads";

    public bool IslTranslationBackfillComplete { get; init; }

    public IReadOnlyList<string> IgnoredChannels { get; init; } = ["Fréttastofa sjónvarps", "Íþróttadeild", "Rás 1", "Sameiginleg markaðsmál", "Rás 2"];

    public bool IsTvdbConfigured => !string.IsNullOrWhiteSpace(TvdbApiKey);

    public bool IsTmdbConfigured => !string.IsNullOrWhiteSpace(TmdbApiKey);

    public bool IsSonarrConfigured => !string.IsNullOrWhiteSpace(SonarrBaseAddress) && !string.IsNullOrWhiteSpace(SonarrApiKey);

    public bool IsReady => IsTvdbConfigured && IsTmdbConfigured && IsSonarrConfigured;

    public string ResolvedEpisodeDownloadDirectory => Path.Join(DownloadsRoot, EpisodeDownloadDirectory);

    public string ResolvedMovieDownloadDirectory => Path.Join(DownloadsRoot, MovieDownloadDirectory);

    public static RuvarrSettings Empty => new();
}
