using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

internal sealed record class RuvTvEpisode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("firstrun")] DateTime FirstRun,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] IReadOnlyList<string> Description,
    [property: JsonPropertyName("file")] Uri File);
