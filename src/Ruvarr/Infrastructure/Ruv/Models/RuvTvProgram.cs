using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

internal sealed record class RuvTvProgram(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("foreign_title")] string? ForeignTitle,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("image")] Uri? Image,
    [property: JsonPropertyName("description")] IReadOnlyList<string> Description,
    [property: JsonPropertyName("multiple_episodes")] bool MultipleEpisodes,
    [property: JsonPropertyName("episodes")] IReadOnlyList<RuvTvEpisode> Episodes,
    [property: JsonPropertyName("web_available_episodes")] int WebAvailableEpisodes,
    [property: JsonPropertyName("channel")] string Channel);
