using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvPanel(
    [property: JsonPropertyName("last_updated")] DateTimeOffset LastUpdated,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("display_style")] string DisplayStyle,
    [property: JsonPropertyName("programs")] IReadOnlyList<RuvTvProgram> Programs);