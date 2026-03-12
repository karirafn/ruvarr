using Ruvarr.Abstractions;

namespace Ruvarr.Infrastructure.Tvdb;

public static class TvdbErrors
{
    public const string SeriesNotFoundCode = "Tvdb.SeriesNotFound";
    public const string EpisodeNotFoundCode = "Tvdb.EpisodeNotFound";

    public static readonly RuvarrError SeriesNotFound = new(SeriesNotFoundCode, "Series was not found in TVDB.");
    public static readonly RuvarrError EpisodeNotFound = new(EpisodeNotFoundCode, "Episode was not found in TVDB.");
}