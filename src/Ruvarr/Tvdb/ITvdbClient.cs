using Ruvarr.Tvdb.Models;

namespace Ruvarr.Tvdb;

public interface ITvdbClient
{
    Task<SeriesData?> GetSeriesAsync(
        int id,
        CancellationToken cancellationToken = default);
    Task<Episode?> GetEpisodeAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<EpisodeTranslation?> GetEpisodeTranslationAsync(
        int id,
        string language = "isl",
        CancellationToken cancellationToken = default);

    Task<SearchResponse> SearchAsync(
        string? query = null,
        string? type = null,
        int? year = null,
        string? company = null,
        string? country = null,
        string? directory = null,
        string? language = null,
        string? network = null,
        string? remoteId = null,
        int? offset = null,
        int? limit = null,
        CancellationToken cancellationToken = default);
}