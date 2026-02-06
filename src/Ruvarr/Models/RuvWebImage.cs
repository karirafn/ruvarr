using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvWebImage(
    [property: JsonPropertyName("uri")] Uri Uri,
    [property: JsonPropertyName("width")] int Width);