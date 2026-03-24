using Ruvarr.Abstractions;

namespace Ruvarr.Settings;

public static class SettingsErrors
{
    public const string InvalidSonarrBaseAddressCode = "Settings.InvalidSonarrBaseAddress";
    public const string InvalidTvdbApiKeyCode = "Settings.InvalidTvdbApiKey";
    public const string InvalidTmdbApiKeyCode = "Settings.InvalidTmdbApiKey";
    public const string EpisodeSubdirectoryAbsoluteCode = "Settings.EpisodeSubdirectoryAbsolute";
    public const string MovieSubdirectoryAbsoluteCode = "Settings.MovieSubdirectoryAbsolute";
    public const string EpisodeSubdirectoryEmptyCode = "Settings.EpisodeSubdirectoryEmpty";
    public const string MovieSubdirectoryEmptyCode = "Settings.MovieSubdirectoryEmpty";
    public const string EpisodeSubdirectoryTraversalCode = "Settings.EpisodeSubdirectoryTraversal";
    public const string MovieSubdirectoryTraversalCode = "Settings.MovieSubdirectoryTraversal";

    public static readonly RuvarrError InvalidSonarrBaseAddress = new(
        InvalidSonarrBaseAddressCode,
        "Sonarr base address must be a valid absolute URI.");

    public static readonly RuvarrError EpisodeSubdirectoryAbsolute = new(
        EpisodeSubdirectoryAbsoluteCode,
        "Episode subdirectory must be a relative path.");

    public static readonly RuvarrError MovieSubdirectoryAbsolute = new(
        MovieSubdirectoryAbsoluteCode,
        "Movie subdirectory must be a relative path.");

    public static readonly RuvarrError EpisodeSubdirectoryEmpty = new(
        EpisodeSubdirectoryEmptyCode,
        "Episode subdirectory is required.");

    public static readonly RuvarrError MovieSubdirectoryEmpty = new(
        MovieSubdirectoryEmptyCode,
        "Movie subdirectory is required.");

    public static readonly RuvarrError EpisodeSubdirectoryTraversal = new(
        EpisodeSubdirectoryTraversalCode,
        "Episode subdirectory must not navigate outside the downloads directory.");

    public static readonly RuvarrError MovieSubdirectoryTraversal = new(
        MovieSubdirectoryTraversalCode,
        "Movie subdirectory must not navigate outside the downloads directory.");
}
