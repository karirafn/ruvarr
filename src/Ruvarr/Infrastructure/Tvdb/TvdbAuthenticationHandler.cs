using System.Net.Http.Headers;

using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

namespace Ruvarr.Infrastructure.Tvdb;

internal sealed class TvdbAuthenticationHandler(IMemoryCache memoryCache, ISettingsStore settingsStore) : DelegatingHandler
{
    private const string AccessTokenCacheKey = "TvdbAccessToken";
    private const string CachedApiKeyCacheKey = "TvdbCachedApiKey";

    // Static so single-flight mutual exclusion survives the handler's transient lifetime.
    private static readonly SemaphoreSlim LoginSemaphore = new(1, 1);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Uri? baseUri = request.RequestUri is { IsAbsoluteUri: true }
            ? new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority))
            : null;

        string token = await GetAccessTokenAsync(baseUri, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(Uri? baseUri, CancellationToken cancellationToken)
    {
        string currentApiKey = settingsStore.Current.TvdbApiKey;

        if (memoryCache.TryGetValue(AccessTokenCacheKey, out string? accessToken)
            && memoryCache.TryGetValue(CachedApiKeyCacheKey, out string? cachedApiKey)
            && cachedApiKey == currentApiKey)
        {
            return accessToken!;
        }

        await LoginSemaphore.WaitAsync(cancellationToken);
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

            AuthenticationResponse? response = await LoginAsync(baseUri, currentApiKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(response?.Data.Token))
            {
                throw new InvalidOperationException("Failed to authenticate with the TVDB");
            }

            accessToken = response.Data.Token;

            // The TVDB access token is valid for 1 month
            MemoryCacheEntryOptions cacheOptions = new()
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
            LoginSemaphore.Release();
        }
    }

    private async Task<AuthenticationResponse?> LoginAsync(Uri? baseUri, string apiKey, CancellationToken cancellationToken)
    {
        Uri loginUri = baseUri is not null
            ? new Uri(baseUri, "v4/login")
            : new Uri("v4/login", UriKind.Relative);

        using HttpRequestMessage loginRequest = new(HttpMethod.Post, loginUri)
        {
            Content = JsonContent.Create(new LoginRequest(apiKey, null), options: RuvarrJson.Default)
        };

        HttpResponseMessage response = await base.SendAsync(loginRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromRuvarrJsonAsync<AuthenticationResponse>(cancellationToken);
    }
}
