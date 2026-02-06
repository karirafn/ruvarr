using System.Text.Json.Serialization;

namespace Ruvarr.Ruv.Models;

public sealed record class RuvImageRenditions(
    [property: JsonPropertyName("web_images")] IReadOnlyList<RuvWebImage> WebImages);