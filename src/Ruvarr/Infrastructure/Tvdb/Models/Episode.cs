using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class Episode(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("seriesId")] int SeriesId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("overview")] string Overview,
    [property: JsonPropertyName("aired")] string Aired,
    [property: JsonPropertyName("runtime")] int? Runtime,
    [property: JsonPropertyName("nameTranslations")] IReadOnlyList<string> NameTranslations,
    [property: JsonPropertyName("overviewTranslations")] IReadOnlyList<string> OverviewTranslations,
    [property: JsonPropertyName("image")] Uri Image,
    [property: JsonPropertyName("imageType")] int? ImageType,
    [property: JsonPropertyName("isMovie")] int IsMovie,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("absoluteNumber")] int AbsoluteNumber,
    [property: JsonPropertyName("seasonNumber")] int SeasonNumber,
    [property: JsonPropertyName("lastUpdated")] string LastUpdated,
    [property: JsonPropertyName("finaleType")] string? FinaleType,
    [property: JsonPropertyName("year")] string Year,
    [property: JsonPropertyName("airsAfterSeason")] int AirsAfterSeason,
    [property: JsonPropertyName("airsBeforeSeason")] int AirsBeforeSeason,
    [property: JsonPropertyName("airsBeforeEpisode")] int AirsBeforeEpisode);