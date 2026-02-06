using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvVodmp4(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("folder")] string Folder,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("multi_bitrate")] bool MultiBitrate,
    [property: JsonPropertyName("expires")] DateOnly Expires,
    [property: JsonPropertyName("quality")] int Quality,
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("scope")] string Scope);