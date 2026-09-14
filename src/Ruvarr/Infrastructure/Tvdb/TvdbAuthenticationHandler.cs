using System.Net;
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

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // LoginAsync sends the login POST via base.SendAsync, so a login request never reaches
            // this override. This guard is defense-in-depth against a future caller routing a login
            // POST through HttpClient.
            bool isLoginRequest = request.Method == HttpMethod.Post
                && (request.RequestUri?.AbsolutePath.EndsWith("/login", StringComparison.Ordinal) ?? false);

            if (!isLoginRequest)
            {
                response.Dispose();

                memoryCache.Remove(AccessTokenCacheKey);
                memoryCache.Remove(CachedApiKeyCacheKey);

                string freshToken = await GetAccessTokenAsync(baseUri, cancellationToken);

                // Preserve the original request semantics on retry — TVDB calls are GETs today,
                // but this handler must not corrupt a future POST's headers, content, or options.
                using HttpRequestMessage retryRequest = new(request.Method, request.RequestUri)
                {
                    Content = request.Content,
                    Version = request.Version,
                    VersionPolicy = request.VersionPolicy,
                };

                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                {
                    retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                foreach (KeyValuePair<string, object?> option in request.Options)
                {
                    retryRequest.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
                }

                retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);

                return await base.SendAsync(retryRequest, cancellationToken);
            }
        }

        return response;
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
