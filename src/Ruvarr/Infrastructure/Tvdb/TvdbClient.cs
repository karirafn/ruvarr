using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Infrastructure.Tvdb;

internal sealed class TvdbClient(ILogger<TvdbClient> logger, HttpClient client) : ApiClient(logger, client), ITvdbClient
{
    public const int MaxPageCount = 20;

    public async Task<SearchResponse> SearchAsync(
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
        CancellationToken cancellationToken = default)
    {
        string path = new TvdbSearchPathBuilder()
            .WithQuery(query)
            .WithType(type)
            .WithYear(year)
            .WithCompany(company)
            .WithCountry(country)
            .WithDirectory(directory)
            .WithLanguage(language)
            .WithNetwork(network)
            .WithRemoteId(remoteId)
            .WithOffset(offset)
            .WithLimit(limit)
            .Build();

        return await GetAsync<SearchResponse>(path, cancellationToken)
            ?? throw new InvalidOperationException("Failed to search the TVDB");
    }

    public async Task<SeriesData?> GetSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        SeriesResponse? firstPage = await GetAsync<SeriesResponse>($"v4/series/{id}/episodes/default?page=0", cancellationToken);

        if (firstPage?.Data is null)
        {
            return null;
        }

        if (firstPage.Links.Next is null)
        {
            return firstPage.Data;
        }

        List<Episode> allEpisodes = [..firstPage.Data.Episodes];
        int page = 1;

        while (page < MaxPageCount)
        {
            SeriesResponse? nextPage = await GetAsync<SeriesResponse>($"v4/series/{id}/episodes/default?page={page}", cancellationToken);

            if (nextPage?.Data is null)
            {
                break;
            }

            allEpisodes.AddRange(nextPage.Data.Episodes);

            if (nextPage.Links.Next is null)
            {
                break;
            }

            page++;
        }

        if (page >= MaxPageCount)
        {
            logger.LogWarning(
                "GetSeriesAsync for series {SeriesId} hit the page cap of {MaxPageCount}. Some episodes may be missing.",
                id,
                MaxPageCount);
        }

        return firstPage.Data with { Episodes = allEpisodes };
    }

    public async Task<Episode?> GetEpisodeAsync(int id, CancellationToken cancellationToken = default) =>
        (await GetAsync<TvdbResponse<Episode?>>($"v4/episodes/{id}", cancellationToken))?.Data;

    public async Task<EpisodeTranslation?> GetEpisodeTranslationAsync(
        int id,
        string language = "isl",
        CancellationToken cancellationToken = default) =>
        (await GetAsync<TvdbResponse<EpisodeTranslation?>>($"v4/episodes/{id}/translations/{language}", cancellationToken))?.Data;
}
