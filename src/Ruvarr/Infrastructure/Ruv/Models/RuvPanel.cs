using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

internal sealed record class RuvPanel(
    [property: JsonPropertyName("programs")] IReadOnlyList<RuvTvProgram> Programs);
