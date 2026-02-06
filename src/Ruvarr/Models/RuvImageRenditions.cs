using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvImageRenditions(
    [property: JsonPropertyName("web_images")] IReadOnlyList<RuvWebImage> WebImages);