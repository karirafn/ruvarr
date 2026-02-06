using System.Text.Json.Serialization;

namespace Ruvarr.Ruv.Models;

public sealed record class RuvClip(
    [property: JsonPropertyName("time")] string Time,
    [property: JsonPropertyName("mark")] int Mark,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("mark_f")] float MarkF,
    [property: JsonPropertyName("slug")] string Slug);