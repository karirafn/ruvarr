using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Ruv.Models;

public sealed record class RuvTvEpisode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("series_id")] int SeriesId,
    [property: JsonPropertyName("firstrun")] DateTime FirstRun,
    [property: JsonPropertyName("file_expires")] DateOnly FileExpires,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("duration")] int Duration,
    [property: JsonPropertyName("duration_friendly")] string DurationFriendly,
    [property: JsonPropertyName("event")] int Event,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("description")] IReadOnlyList<string> Description,
    [property: JsonPropertyName("image_renditions")] RuvImageRenditions ImageRenditions,
    [property: JsonPropertyName("image")] Uri Image,
    [property: JsonPropertyName("image_og")] Uri ImageOg,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("subtitles_url")] Uri SubtitlesUrl,
    [property: JsonPropertyName("subtitles")] RuvSubtitles Subtitles,
    [property: JsonPropertyName("open_subtitles")] bool OpenSubtitles,
    [property: JsonPropertyName("closed_subtitles")] bool ClosedSubtitles,
    [property: JsonPropertyName("auto_subtitles")] bool AutoSubtitles,
    [property: JsonPropertyName("file")] Uri File,
    [property: JsonPropertyName("temp")] RuvTemp Temp,
    [property: JsonPropertyName("clips")] IReadOnlyList<RuvClip> Clips,
    [property: JsonPropertyName("files")] RuvFiles Files,
    [property: JsonPropertyName("credit_point")] int CreditPoint);