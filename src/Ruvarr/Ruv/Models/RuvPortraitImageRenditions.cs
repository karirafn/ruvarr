using System.Text.Json.Serialization;

namespace Ruvarr.Ruv.Models;

public sealed record class RuvPortraitImageRenditions(
    [property: JsonPropertyName("web_images")] IReadOnlyList<RuvWebImage> WebImages);