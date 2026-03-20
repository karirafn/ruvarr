using System.Net.Http.Headers;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Infrastructure.Tvdb;

internal sealed class TvdbClient(HttpClient client, IMemoryCache memoryCache, IOptions<TvdbOptions> options) : ITvdbClient
{
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

        await SetAuthorizationHeader(cancellationToken);

        SearchResponse response = await client.GetFromJsonAsync<SearchResponse>(path, cancellationToken)
            ?? throw new InvalidOperationException("Failed to search the TVDB");

        return response;
    }

    public async Task<SeriesData?> GetSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        await SetAuthorizationHeader(cancellationToken);

        SeriesResponse? response = await client.GetFromJsonAsync<SeriesResponse>($"v4/series/{id}/episodes/default", cancellationToken);

        return response?.Data;
    }

    public async Task<Episode?> GetEpisodeAsync(int id, CancellationToken cancellationToken = default)
    {
        await SetAuthorizationHeader(cancellationToken);

        TvdbResponse<Episode?>? response = await client.GetFromJsonAsync<TvdbResponse<Episode?>>(
            $"v4/episodes/{id}",
            cancellationToken);

        return response?.Data;
    }

    public async Task<EpisodeTranslation?> GetEpisodeTranslationAsync(int id, string language = "isl", CancellationToken cancellationToken = default)
    {
        await SetAuthorizationHeader(cancellationToken);

        TvdbResponse<EpisodeTranslation?>? response = await client.GetFromJsonAsync<TvdbResponse<EpisodeTranslation?>>(
            $"v4/episodes/{id}/translations/{language}",
            cancellationToken);

        return response?.Data;
    }

    private async Task SetAuthorizationHeader(CancellationToken cancellationToken)
    {
        if (!memoryCache.TryGetValue(TvdbOptions.AccessTokenCacheKey, out string? accessToken))
        {
            AuthenticationResponse? response = await LoginAsync(options.Value.ApiKey, null, cancellationToken);

            if (string.IsNullOrWhiteSpace(response?.Data.Token))
            {
                return;
            }

            accessToken = response.Data.Token;

            // The TVDB access token is valid for 1 month
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddDays(28),
                Size = 1
            };
            memoryCache.Set(TvdbOptions.AccessTokenCacheKey, accessToken, cacheOptions);
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

    }

    private async Task<AuthenticationResponse?> LoginAsync(string apiKey, string? pin = null, CancellationToken cancellationToken = default)
    {
        LoginRequest request = new(apiKey, pin);
        HttpResponseMessage response = await client.PostAsJsonAsync("v4/login", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        AuthenticationResponse? content = await response.Content.ReadFromJsonAsync<AuthenticationResponse>(cancellationToken);

        return content;
    }
}