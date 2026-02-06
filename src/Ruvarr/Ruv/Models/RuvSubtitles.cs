using System.Text.Json.Serialization;

namespace Ruvarr.Ruv.Models;

public sealed record class RuvSubtitles(
    [property: JsonPropertyName("is")] Uri Is);