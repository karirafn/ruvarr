using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class Status(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("recordType")] string RecordType,
    [property: JsonPropertyName("keepUpdated")] bool KeepUpdated);