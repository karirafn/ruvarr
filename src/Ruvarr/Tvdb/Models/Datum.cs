using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class Datum(
    [property: JsonPropertyName("objectID")] string ObjectId,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string> Aliases,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("Id")] string Id,
    [property: JsonPropertyName("image_url")] Uri ImageUrl,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("first_air_time")] DateOnly FirstAirDate,
    [property: JsonPropertyName("overview")] string Overview,
    [property: JsonPropertyName("primary_language")] string PrimaryLanguage,
    [property: JsonPropertyName("primary_type")] string PrimaryType,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("Type")] string Type,
    [property: JsonPropertyName("tvdb_id")] string TvdbId,
    [property: JsonPropertyName("year")] string Year,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("overviews")] IReadOnlyDictionary<string, string> Overviews,
    [property: JsonPropertyName("translations")] IReadOnlyDictionary<string, string> Translations,
    [property: JsonPropertyName("network")] string Network,
    [property: JsonPropertyName("remote_ids")] IReadOnlyList<RemoteId> RemoteIds);
