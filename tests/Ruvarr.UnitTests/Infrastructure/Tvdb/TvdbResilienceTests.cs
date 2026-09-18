// Tests the resilience pipeline wired into the DI-built ITvdbClient by calling the
// real AddTvdb() registration and substituting the primary HTTP handler via
// ConfigurePrimaryHttpMessageHandler. No Docker or database is required.
//
// Key assertions:
// - A [503, 200] sequence retries to success and the retried request carries
//   Authorization: Bearer <token>, proving resilience is outermost (auth handler
//   runs on every attempt, including the retry).
// - A single 401 response is handled entirely by TvdbAuthenticationHandler
//   (one re-login + one retry) and is NOT retried by the resilience pipeline
//   (401 is not in the transient set). This proves the pipeline does not
//   double-count the auth handler's own retry.

using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http.Resilience;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;
using Ruvarr.UnitTests.Infrastructure.Resilience;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb;

public sealed class TvdbResilienceTests
{
    private const string TvdbApiKey = "test-tvdb-api-key";
    private const string CachedToken = "cached-bearer-token";
    private const string AccessTokenCacheKey = "TvdbAccessToken";
    private const string CachedApiKeyCacheKey = "TvdbCachedApiKey";

    // Seed the memory cache so TvdbAuthenticationHandler returns a bearer token
    // without hitting a real login endpoint.
    private static MemoryCache BuildCacheWithToken(string apiKey, string token)
    {
        MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });
        MemoryCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(28),
            Size = 1
        };
        cache.Set(AccessTokenCacheKey, token, cacheOptions);
        cache.Set(CachedApiKeyCacheKey, apiKey, cacheOptions);
        return cache;
    }

    // TDD cycle (a): a [503, 200] sequence retries to success; the retried request
    // carries Authorization: Bearer <token>, proving resilience wraps the auth handler.
    [Fact]
    public async Task WhenTransient503ThenSuccess_ClientSucceedsAndRetryCarriesBearerToken()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: TvdbApiKey));

        using MemoryCache cache = BuildCacheWithToken(TvdbApiKey, CachedToken);

        // Script [503, 200] — first attempt fails, pipeline retries, second succeeds.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        ServiceCollection services = new();
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton(settingsStore);
        services.AddTransient<TvdbAuthenticationHandler>();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tvdb:BaseAddress"] = "https://api4.thetvdb.com/",
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddTvdb();

        // Post-configure to sub-second delays: keeps Total >= Attempt and
        // SamplingDuration >= 2 * Attempt so startup validation passes.
        // AddStandardResilienceHandler registers options under "{clientName}-standard".
        string clientName = nameof(ITvdbClient);
        services.Configure<HttpStandardResilienceOptions>($"{clientName}-standard", options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        // Substitute the primary handler after AddTvdb so it is innermost.
        services.AddHttpClient<ITvdbClient, TvdbClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://api4.thetvdb.com/v4/series/1",
            CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2, "pipeline should retry once and succeed on the second attempt");

        // The retried (second) request must carry Authorization: Bearer <token>,
        // proving resilience is outermost and the auth handler ran on the retry.
        HttpRequestMessage retriedRequest = handler.CapturedRequests[1];
        AuthenticationHeaderValue? auth = retriedRequest.Headers.Authorization;
        auth.ShouldNotBeNull("TvdbAuthenticationHandler should have attached Authorization on retry");
        auth.Scheme.ShouldBe("Bearer");
        auth.Parameter.ShouldBe(CachedToken);
    }

    // TDD cycle (b): a single 401 is handled by TvdbAuthenticationHandler (its own
    // one re-login + one retry), and the resilience pipeline does NOT retry it — the
    // primary handler is invoked exactly twice (original + auth-handler retry), not more.
    //
    // ScriptedWithLoginFakeHandler is kept here (not consolidated into ScriptedHttpMessageHandler)
    // because it has a genuinely distinct concern: it must distinguish login POST requests from
    // application GET requests and respond differently to each. ScriptedHttpMessageHandler is
    // designed for a single uniform scripted sequence and cannot express this routing logic cleanly.
    [Fact]
    public async Task WhenSingle401_AuthHandlerRetriesOnceAndPipelineDoesNotRetry()
    {
        // Arrange
        const string FreshToken = "fresh-token-after-401-relogin";
        string freshLoginJson = $$"""{"data":{"token":"{{FreshToken}}"},"status":"success"}""";

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: TvdbApiKey));

        using MemoryCache cache = BuildCacheWithToken(TvdbApiKey, CachedToken);

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tvdb:BaseAddress"] = "https://api4.thetvdb.com/",
            })
            .Build();

        int applicationCallCount = 0;

        // Fake handles login POSTs and application GETs. The first application request
        // returns 401 so the auth handler refreshes its token; the retry returns 200.
        using ScriptedWithLoginFakeHandler fake = new(
            freshLoginJson: freshLoginJson,
            getApplicationStatus: () =>
            {
                applicationCallCount++;
                return applicationCallCount == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
            });

        ServiceCollection services = new();
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton(settingsStore);
        services.AddTransient<TvdbAuthenticationHandler>();
        services.AddSingleton(configuration);
        services.AddTvdb();

        // AddStandardResilienceHandler registers options under "{clientName}-standard".
        string clientName = nameof(ITvdbClient);
        services.Configure<HttpStandardResilienceOptions>($"{clientName}-standard", options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<ITvdbClient, TvdbClient>()
            .ConfigurePrimaryHttpMessageHandler(() => fake);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://api4.thetvdb.com/v4/series/1",
            CancellationToken.None);

        // Assert — the overall call succeeds (auth handler recovered via relogin + retry)
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The primary handler received exactly 2 application requests:
        //   1. original (→ 401, auth handler refreshes token and retries)
        //   2. auth-handler retry (→ 200)
        // The resilience pipeline must NOT have added more retries on top of this.
        applicationCallCount.ShouldBe(2,
            "401 is not in the transient set — resilience must not retry it; auth handler owns the single 401 refresh");
    }

    /// <summary>
    /// Handles login POSTs (from TvdbAuthenticationHandler) and application GET requests
    /// with a caller-supplied status selector. Used for the 401-pipeline-isolation test.
    /// Not consolidated into ScriptedHttpMessageHandler because it must route between login
    /// and application requests — a concern ScriptedHttpMessageHandler cannot express cleanly.
    /// </summary>
    private sealed class ScriptedWithLoginFakeHandler(string freshLoginJson, Func<HttpStatusCode> getApplicationStatus)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            bool isLogin = request.Method == HttpMethod.Post
                && (request.RequestUri?.AbsolutePath.Contains("login", StringComparison.Ordinal) ?? false);

            if (isLogin)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(freshLoginJson, Encoding.UTF8, "application/json"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(getApplicationStatus())
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
