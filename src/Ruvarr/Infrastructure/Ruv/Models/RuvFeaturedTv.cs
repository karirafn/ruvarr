using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

internal sealed record class RuvFeaturedTv(
    [property: JsonPropertyName("panels")] IReadOnlyList<RuvPanel> Panels);
