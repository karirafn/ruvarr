using System.Net.Http.Headers;

using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

namespace Ruvarr.Infrastructure.Tvdb;

internal sealed class TvdbClient(HttpClient client, IMemoryCache memoryCache, ISettingsStore settingsStore) : ITvdbClient, IDisposable
{
    internal const string AccessTokenCacheKey = "TvdbAccessToken";
    private const string CachedApiKeyCacheKey = "TvdbCachedApiKey";

    private readonly SemaphoreSlim _loginSemaphore = new(1, 1);

    public void Dispose() => _loginSemaphore.Dispose();
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

        string token = await GetAccessTokenAsync(cancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Failed to search the TVDB");
    }

    public async Task<SeriesData?> GetSeriesAsync(int id, CancellationToken cancellationToken = default)
    {
        string token = await GetAccessTokenAsync(cancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Get, $"v4/series/{id}/episodes/default");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        SeriesResponse? seriesResponse = await response.Content.ReadFromJsonAsync<SeriesResponse>(cancellationToken);

        return seriesResponse?.Data;
    }

    public async Task<Episode?> GetEpisodeAsync(int id, CancellationToken cancellationToken = default)
    {
        string token = await GetAccessTokenAsync(cancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Get, $"v4/episodes/{id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        TvdbResponse<Episode?>? episodeResponse = await response.Content.ReadFromJsonAsync<TvdbResponse<Episode?>>(cancellationToken);

        return episodeResponse?.Data;
    }

    public async Task<EpisodeTranslation?> GetEpisodeTranslationAsync(int id, string language = "isl", CancellationToken cancellationToken = default)
    {
        string token = await GetAccessTokenAsync(cancellationToken);

        using HttpRequestMessage request = new(HttpMethod.Get, $"v4/episodes/{id}/translations/{language}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        TvdbResponse<EpisodeTranslation?>? translationResponse = await response.Content.ReadFromJsonAsync<TvdbResponse<EpisodeTranslation?>>(cancellationToken);

        return translationResponse?.Data;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        string currentApiKey = settingsStore.Current.TvdbApiKey;

        if (memoryCache.TryGetValue(AccessTokenCacheKey, out string? accessToken)
            && memoryCache.TryGetValue(CachedApiKeyCacheKey, out string? cachedApiKey)
            && cachedApiKey == currentApiKey)
        {
            return accessToken!;
        }

        await _loginSemaphore.WaitAsync(cancellationToken);
        try
        {
            currentApiKey = settingsStore.Current.TvdbApiKey;

            if (memoryCache.TryGetValue(AccessTokenCacheKey, out accessToken)
                && memoryCache.TryGetValue(CachedApiKeyCacheKey, out cachedApiKey)
                && cachedApiKey == currentApiKey)
            {
                return accessToken!;
            }

            memoryCache.Remove(AccessTokenCacheKey);
            memoryCache.Remove(CachedApiKeyCacheKey);

            AuthenticationResponse? response = await LoginAsync(currentApiKey, null, cancellationToken);

            if (string.IsNullOrWhiteSpace(response?.Data.Token))
            {
                throw new InvalidOperationException("Failed to authenticate with the TVDB");
            }

            accessToken = response.Data.Token;

            // The TVDB access token is valid for 1 month
            MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTimeOffset.Now.AddDays(28),
                Size = 1
            };
            memoryCache.Set(AccessTokenCacheKey, accessToken, cacheOptions);
            memoryCache.Set(CachedApiKeyCacheKey, currentApiKey, cacheOptions);

            return accessToken;
        }
        finally
        {
            _loginSemaphore.Release();
        }
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