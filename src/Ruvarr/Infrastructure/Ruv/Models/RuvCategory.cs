using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvCategory(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string Slug);