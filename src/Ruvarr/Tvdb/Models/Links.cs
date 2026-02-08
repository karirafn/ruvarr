using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class Links(
    [property: JsonPropertyName("prev")] Uri? Prev,
    [property: JsonPropertyName("self")] Uri Self,
    [property: JsonPropertyName("next")] Uri? Next,
    [property: JsonPropertyName("total_items")] int TotalItems,
    [property: JsonPropertyName("page_size")] int PageSize);
