using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class EpisodeTranslationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("Data")] EpisodeTranslation Data);