using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class Series(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("image")] Uri Image,
    [property: JsonPropertyName("nameTranslations")] IReadOnlyList<string> NameTranslations,
    [property: JsonPropertyName("overviewTranslations")] IReadOnlyList<string> OverviewTranslations,
    [property: JsonPropertyName("episodes")] IReadOnlyList<Episode> Episodes,
    [property: JsonPropertyName("aliases")] IReadOnlyList<TvdbAlias> Aliases,
    [property: JsonPropertyName("firstAired")] string FirstAired,
    [property: JsonPropertyName("lastAired")] string LastAired,
    [property: JsonPropertyName("nextAired")] string NextAired,
    [property: JsonPropertyName("score")] int Score,
    [property: JsonPropertyName("status")] Status Status,
    [property: JsonPropertyName("originalCountry")] string OriginalCountry,
    [property: JsonPropertyName("originalLanguage")] string OriginalLanguage,
    [property: JsonPropertyName("isOrderRandomized")] bool IsOrderRandomized,
    [property: JsonPropertyName("lastUpdated")] string LastUpdated,
    [property: JsonPropertyName("averageRuntime")] int? AverageRuntime,
    [property: JsonPropertyName("overview")] string Overview,
    [property: JsonPropertyName("year")] string Year);