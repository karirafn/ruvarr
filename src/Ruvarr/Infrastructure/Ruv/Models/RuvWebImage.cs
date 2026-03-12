using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvWebImage(
    [property: JsonPropertyName("uri")] Uri Uri,
    [property: JsonPropertyName("width")] int Width);