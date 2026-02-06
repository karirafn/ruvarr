using System.Text.Json.Serialization;

namespace Ruvarr.Models;

public sealed record class RuvTvProgram(
    [property: JsonPropertyName("last_updated")] DateTimeOffset LastUpdated,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("foreign_title")] string ForeignTitle,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("image_renditions")] RuvImageRenditions ImageRenditions,
    [property: JsonPropertyName("image")] Uri Image,
    [property: JsonPropertyName("image_og")] Uri ImageOg,
    [property: JsonPropertyName("portrait_image_renditions")] RuvPortraitImageRenditions PortraitImageRenditions,
    [property: JsonPropertyName("portrait_image")] Uri PortraitImage,
    [property: JsonPropertyName("description")] IReadOnlyList<string> Description,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("categories")] IReadOnlyList<RuvCategory> Categories,
    [property: JsonPropertyName("division")] string Division,
    [property: JsonPropertyName("multiple_episodes")] bool MultipleEpisodes,
    [property: JsonPropertyName("episodes")] IReadOnlyList<RuvEpisode> Episodes,
    [property: JsonPropertyName("reverse_episode_order")] bool ReverseEpisodeOrder,
    [property: JsonPropertyName("web_available_episodes")] int WebAvailableEpisodes,
    [property: JsonPropertyName("vod_avilable_episodes")] int VodAvailableEpisodes,
    [property: JsonPropertyName("podcast_available_episodes")] int PodcastVailableEpisodes,
    [property: JsonPropertyName("web_latest_date")] DateTime WebLatestDate,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("web_player_url")] Uri WebPlayerUrl);