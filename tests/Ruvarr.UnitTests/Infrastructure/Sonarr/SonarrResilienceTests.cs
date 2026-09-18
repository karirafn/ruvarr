// Tests the resilience pipeline wired into the DI-built SonarrClient by calling the
// real AddSonarr() registration and substituting the primary HTTP handler via
// ConfigurePrimaryHttpMessageHandler. No Docker or database is required.
// The key assertion beyond retry success is that the retried request still carries
// the rewritten URI and X-Api-Key header from SonarrDelegatingHandler, proving the
// resilience handler is outermost (wraps the delegating handler).

using System.Net;

using Microsoft.Extensions.Http.Resilience;

using NSubstitute;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Settings;
using Ruvarr.UnitTests.Infrastructure.Resilience;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr;

public sealed class SonarrResilienceTests
{
    private const string SonarrBaseAddress = "http://sonarr.local:8989/";
    private const string SonarrApiKey = "test-api-key-abc123";
    private const string ClientName = nameof(SonarrClient);

    private static ServiceProvider BuildProvider(ScriptedHttpMessageHandler handler)
    {
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings
        {
            SonarrBaseAddress = SonarrBaseAddress,
            SonarrApiKey = SonarrApiKey,
        });

        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddSingleton(settingsStore);
        services.AddSonarr();

        // Post-configure to sub-second delays so the suite stays fast.
        // Invariants preserved: Total >= Attempt and SamplingDuration >= 2 * Attempt.
        services.Configure<HttpStandardResilienceOptions>($"{ClientName}-standard", options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<SonarrClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    // GET [503, 200] — the pipeline retries safe methods, so the second attempt succeeds.
    // Also proves the resilience handler wraps SonarrDelegatingHandler (retried request
    // still carries the rewritten host and X-Api-Key from the inner delegating handler).
    [Fact]
    public async Task WhenGetReturnsTransient503ThenSuccess_ClientSucceedsAndDelegatingHandlerRewroteRetry()
    {
        // Arrange — script [503, 200]; GET is safe so the pipeline retries.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        await using ServiceProvider provider = BuildProvider(handler);
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(ClientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://unconfigured/api/v3/episode?seriesId=1",
            CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2, "pipeline should retry GET once and succeed on the second attempt");

        // The retried (second) request must still carry the rewritten host and X-Api-Key,
        // proving resilience is outermost and the delegating handler ran on the retry.
        HttpRequestMessage retriedRequest = handler.CapturedRequests[1];
        retriedRequest.RequestUri.ShouldNotBeNull();
        retriedRequest.RequestUri.Host.ShouldBe("sonarr.local",
            "SonarrDelegatingHandler should have rewritten the URI on retry");
        retriedRequest.Headers.TryGetValues("X-Api-Key", out IEnumerable<string>? keyValues).ShouldBeTrue(
            "SonarrDelegatingHandler should have stamped X-Api-Key on retry");
        keyValues.ShouldNotBeNull();
        keyValues.ShouldContain(SonarrApiKey);
    }

    // POST [503, 200] — the pipeline must NOT retry unsafe methods, so the 503 surfaces
    // directly (RequestCount == 1). This prevents duplicate Sonarr commands on transient
    // server errors (ManualImportFilesAsync / AddSeriesAsync both POST).
    [Fact]
    public async Task WhenPostReturns503_IsNotRetriedAndFailureSurfaces()
    {
        // Arrange — script [503, 200]; if the pipeline incorrectly retries the POST,
        // RequestCount will be 2 and the response will be 200 instead of 503.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        await using ServiceProvider provider = BuildProvider(handler);
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(ClientName);

        // Act
        using StringContent body = new("{}");
        using HttpResponseMessage response = await client.PostAsync(
            "https://unconfigured/api/v3/command",
            body,
            CancellationToken.None);

        // Assert — the 503 is returned directly; no retry was attempted.
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
            "POST must not be retried — DisableForUnsafeHttpMethods must be set on the retry options");
        handler.RequestCount.ShouldBe(1, "POST must not be retried; RequestCount == 1 proves no retry occurred");
    }
}
