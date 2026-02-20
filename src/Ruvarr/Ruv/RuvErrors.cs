using Ruvarr.Abstractions;

namespace Ruvarr.Ruv;

public static class RuvErrors
{
    public const string EpisodeNotFoundCode = "Episodes.NotFound";

    public static RuvarrError EpisodeNotFound => new(EpisodeNotFoundCode, "Episode not found.");
}