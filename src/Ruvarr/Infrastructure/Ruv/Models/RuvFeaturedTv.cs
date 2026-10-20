using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvFeaturedTv(
    [property: JsonPropertyName("panels")] IReadOnlyList<RuvPanel> Panels);
