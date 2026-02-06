using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvFiles(
    [property: JsonPropertyName("vodmp4")] RuvVodmp4 VodMp4);