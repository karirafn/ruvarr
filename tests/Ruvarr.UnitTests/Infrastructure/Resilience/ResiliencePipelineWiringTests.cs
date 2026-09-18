// Asserts that each in-scope HTTP client (IRuvClient, SonarrClient, ITvdbClient) has a
// resilience pipeline handler wired, and that the out-of-scope IRuvStreamInspector does not.
//
// Approach (Decision 5 from the design): build each client via the real DI registration,
// substitute the primary handler with a ScriptedHttpMessageHandler, and assert observable
// pipeline behaviour (retry count). This is more reliable than inspecting private DI
// descriptor types and proves the pipeline end-to-end, not just registration presence.
//
// AC #2 (circuit breaker): the standard pipeline includes a circuit breaker by default.
// Tripping the real breaker requires ≥ 100 requests in a 30s sampling window
// (MinimumThroughput default), which is impractical in a unit test. AC #2 is satisfied by:
//   (a) This wiring assertion — the resilience pipeline (which contains the standard breaker)
//       is confirmed active via the retry behaviour observable here.
//   (b) The standard handler's own test suite (platform-tested behaviour, not re-asserted here).
//
// No Docker or database is required.

using System.Net;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http.Resilience;

using NSubstitute;

using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Resilience;

public sealed class ResiliencePipelineWiringTests
{
    // Sub-second options used for all wiring tests: keeps Total >= Attempt and
    // SamplingDuration >= 2 * Attempt so startup validation passes.
    private static void ConfigureFastOptions(IServiceCollection services, string clientName)
    {
        services.Configure<HttpStandardResilienceOptions>($"{clientName}-standard", options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });
    }

    // TDD cycle (a): IRuvClient has a resilience handler — a [503, 200] sequence retries
    // to success (RequestCount == 2), proving the retry pipeline is active.
    // The standard pipeline also wires the circuit breaker (AC #2 by configuration).
    [Fact]
    public async Task WhenRuvClientRegistered_ResiliencePipelineIsActive()
    {
        // Arrange
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ruv:BaseAddress"] = "https://api.ruv.is/",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        string clientName = nameof(IRuvClient);
        ConfigureFastOptions(services, clientName);

        services.AddHttpClient<IRuvClient, RuvClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://api.ruv.is/api/programs/program/1234/all",
            CancellationToken.None);

        // Assert — the pipeline retried the 503 and returned the 200 on the second attempt.
        // RequestCount == 2 proves the resilience pipeline (including the circuit breaker) is active.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2,
            "AddRuvarrResilience must wire the standard resilience pipeline (including circuit breaker) to IRuvClient; RequestCount == 2 proves a retry occurred");
    }

    // TDD cycle (b): SonarrClient has a resilience handler.
    [Fact]
    public async Task WhenSonarrClientRegistered_ResiliencePipelineIsActive()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings
        {
            SonarrBaseAddress = "http://sonarr.local:8989/",
            SonarrApiKey = "test-api-key",
        });

        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddSingleton(settingsStore);
        services.AddSonarr();

        string clientName = nameof(SonarrClient);
        ConfigureFastOptions(services, clientName);

        services.AddHttpClient<SonarrClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://unconfigured/api/v3/episode?seriesId=1",
            CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2,
            "AddRuvarrResilience must wire the standard resilience pipeline (including circuit breaker) to SonarrClient; RequestCount == 2 proves a retry occurred");
    }

    // TDD cycle (c): ITvdbClient has a resilience handler.
    [Fact]
    public async Task WhenTvdbClientRegistered_ResiliencePipelineIsActive()
    {
        // Arrange
        const string TvdbApiKey = "test-tvdb-key";
        const string CachedToken = "cached-token";

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: TvdbApiKey));

        MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });
        MemoryCacheEntryOptions cacheEntryOptions = new()
        {
            AbsoluteExpiration = DateTimeOffset.UtcNow.AddDays(28),
            Size = 1
        };
        cache.Set("TvdbAccessToken", CachedToken, cacheEntryOptions);
        cache.Set("TvdbCachedApiKey", TvdbApiKey, cacheEntryOptions);

        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tvdb:BaseAddress"] = "https://api4.thetvdb.com/",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton<IMemoryCache>(cache);
        services.AddSingleton(settingsStore);
        services.AddTransient<TvdbAuthenticationHandler>();
        services.AddSingleton(configuration);
        services.AddTvdb();

        string clientName = nameof(ITvdbClient);
        ConfigureFastOptions(services, clientName);

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
        handler.RequestCount.ShouldBe(2,
            "AddRuvarrResilience must wire the standard resilience pipeline (including circuit breaker) to ITvdbClient; RequestCount == 2 proves a retry occurred");

        cache.Dispose();
    }

    // TDD cycle (d): IRuvStreamInspector has NO resilience handler — a 503 response is
    // returned directly (no retry), proving the pipeline is absent on this client.
    // This is the mechanical guard: no future edit should drop the pipeline from an
    // in-scope client or accidentally add one to the stream inspector.
    [Fact]
    public async Task WhenRuvStreamInspectorRegistered_NoResiliencePipelineIsActive()
    {
        // Arrange — script [503, 200]; without a resilience pipeline, the 503 is returned
        // directly and no retry is attempted (RequestCount remains 1).
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ruv:BaseAddress"] = "https://api.ruv.is/",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        services.AddHttpClient<IRuvStreamInspector, RuvStreamInspector>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(nameof(IRuvStreamInspector));

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://api.ruv.is/api/programs/program/1234/all",
            CancellationToken.None);

        // Assert — the 503 is returned as-is (no retry), proving no resilience pipeline is wired.
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
            "IRuvStreamInspector must not have a resilience pipeline; a 503 should be returned directly without retry");
        handler.RequestCount.ShouldBe(1,
            "IRuvStreamInspector must not retry — RequestCount == 1 proves no resilience pipeline is present");
    }
}
