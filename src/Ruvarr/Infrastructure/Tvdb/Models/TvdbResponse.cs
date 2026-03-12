using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class TvdbResponse<T>(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("Data")] T Data);