using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class SearchResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] IReadOnlyList<Datum> Data,
    [property: JsonPropertyName("links")] Links Links);