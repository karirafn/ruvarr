using System.Text.Json.Serialization;

namespace Ruvarr.Infrastructure.Sonarr.Models;

internal sealed record class AddSeriesRequest(
    [property: JsonPropertyName("tvdbId")] int TvdbId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("qualityProfileId")] int QualityProfileId,
    [property: JsonPropertyName("rootFolderPath")] string RootFolderPath,
    [property: JsonPropertyName("monitored")] bool Monitored,
    [property: JsonPropertyName("seasonFolder")] bool SeasonFolder,
    [property: JsonPropertyName("addOptions")] AddSeriesOptions AddOptions);

internal sealed record class AddSeriesOptions(
    [property: JsonPropertyName("searchForMissingEpisodes")] bool SearchForMissingEpisodes);
