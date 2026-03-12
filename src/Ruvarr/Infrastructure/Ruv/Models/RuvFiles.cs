using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvFiles(
    [property: JsonPropertyName("vodmp4")] RuvVodmp4 VodMp4);