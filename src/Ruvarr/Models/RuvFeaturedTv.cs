using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvFeaturedTv(
    [property: JsonPropertyName("last_updated")] DateTimeOffset LastUpdated,
    [property: JsonPropertyName("panels")] IReadOnlyList<RuvPanel> Panels);