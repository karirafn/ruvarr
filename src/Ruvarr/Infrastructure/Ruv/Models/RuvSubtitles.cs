using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvSubtitles(
    [property: JsonPropertyName("is")] Uri Is);