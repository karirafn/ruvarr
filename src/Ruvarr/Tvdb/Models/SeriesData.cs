using System.Text.Json.Serialization;

namespace Ruvarr.Tvdb.Models;

public sealed record class SeriesData(
    [property: JsonPropertyName("series")] Series Series,
    [property: JsonPropertyName("episodes")] IReadOnlyList<Episode> Episodes);