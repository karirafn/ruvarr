using System.Text.Json.Serialization;

namespace Ruvarr.Ruv.Models;

public sealed record class RuvTemp(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("folder")] string Folder);