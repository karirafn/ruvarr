using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class RemoteId(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("sourceName")] string SourceName);