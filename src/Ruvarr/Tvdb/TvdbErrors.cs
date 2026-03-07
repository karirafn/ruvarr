using Ruvarr.Abstractions;

namespace Ruvarr.Tvdb;

public static class TvdbErrors
{
    public const string EpisodeNotFoundCode = "Tvdb.EpisodeNotFound";

    public static readonly RuvarrError EpisodeNotFound = new(EpisodeNotFoundCode, "The episode was not found in Tvdb.");
}