using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvSubtitles(
    [property: JsonPropertyName("is")] Uri Is);