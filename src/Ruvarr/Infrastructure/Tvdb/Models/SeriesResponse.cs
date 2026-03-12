using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class SeriesResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("data")] SeriesData? Data,
    [property: JsonPropertyName("links")] Links Links);