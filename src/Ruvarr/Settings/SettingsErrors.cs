using Ruvarr.Abstractions;

namespace Ruvarr.Settings;

public static class SettingsErrors
{
    public const string InvalidSonarrBaseAddressCode = "Settings.InvalidSonarrBaseAddress";
    public const string InvalidTvdbApiKeyCode = "Settings.InvalidTvdbApiKey";
    public const string InvalidTmdbApiKeyCode = "Settings.InvalidTmdbApiKey";
    public const string EpisodeSubdirectoryAbsoluteCode = "Settings.EpisodeSubdirectoryAbsolute";
    public const string MovieSubdirectoryAbsoluteCode = "Settings.MovieSubdirectoryAbsolute";

    public static readonly RuvarrError InvalidSonarrBaseAddress = new(
        InvalidSonarrBaseAddressCode,
        "Sonarr base address must be a valid absolute URI.");

    public static readonly RuvarrError EpisodeSubdirectoryAbsolute = new(
        EpisodeSubdirectoryAbsoluteCode,
        "Episode subdirectory must be a relative path.");

    public static readonly RuvarrError MovieSubdirectoryAbsolute = new(
        MovieSubdirectoryAbsoluteCode,
        "Movie subdirectory must be a relative path.");
}
