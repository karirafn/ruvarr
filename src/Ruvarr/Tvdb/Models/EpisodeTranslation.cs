using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class EpisodeTranslation(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("overview")] string Overview,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("isPrimary")] bool IsPrimary);