using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvPortraitImageRenditions(
    [property: JsonPropertyName("web_images")] IReadOnlyList<RuvWebImage> WebImages);