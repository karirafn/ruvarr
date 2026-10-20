using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvPanel(
    [property: JsonPropertyName("programs")] IReadOnlyList<RuvTvProgram> Programs);
