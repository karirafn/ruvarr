using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Tvdb.Models;

public sealed record class TvdbAlias(
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("name")] string Name);