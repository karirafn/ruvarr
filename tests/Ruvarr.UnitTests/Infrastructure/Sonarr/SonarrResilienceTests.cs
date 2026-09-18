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

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr;

public sealed class SonarrResilienceTests
{
    private const string SonarrBaseAddress = "http://sonarr.local:8989/";
    private const string SonarrApiKey = "test-api-key-abc123";

    [Fact]
    public async Task WhenTransient503ThenSuccess_ClientSucceedsAndDelegatingHandlerRewroteRetry()
    {
        // Arrange — configure ISettingsStore so SonarrDelegatingHandler rewrites the URI
        // and stamps the X-Api-Key header on every attempt (including the retried one).
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings
        {
            SonarrBaseAddress = SonarrBaseAddress,
            SonarrApiKey = SonarrApiKey,
        });

        // Script [503, 200] — first attempt fails, pipeline retries, second succeeds.
        Queue<HttpStatusCode> script = new([HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        List<HttpRequestMessage> capturedRequests = [];
        int callCount = 0;

        using CapturingFakeHandler fake = new(request =>
        {
            callCount++;
            capturedRequests.Add(request);
            return script.Count > 0 ? script.Dequeue() : HttpStatusCode.OK;
        });

        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddSingleton(settingsStore);
        services.AddSonarr();

        // Post-configure to sub-second delays: keeps Total >= Attempt and
        // SamplingDuration >= 2 * Attempt so startup validation passes.
        string clientName = nameof(SonarrClient);
        services.Configure<HttpStandardResilienceOptions>(clientName, options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        // Substitute the primary handler after AddSonarr so it is innermost.
        services.AddHttpClient<SonarrClient>()
            .ConfigurePrimaryHttpMessageHandler(() => fake);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act — send a request through the full pipeline.
        using HttpResponseMessage response = await client.GetAsync(
            "https://unconfigured/api/v3/episode?seriesId=1",
            CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        callCount.ShouldBe(2, "pipeline should retry once and succeed on the second attempt");

        // The retried (second) request must still carry the rewritten host and X-Api-Key,
        // proving resilience is outermost and the delegating handler ran on the retry.
        HttpRequestMessage retriedRequest = capturedRequests[1];
        retriedRequest.RequestUri.ShouldNotBeNull();
        retriedRequest.RequestUri.Host.ShouldBe("sonarr.local",
            "SonarrDelegatingHandler should have rewritten the URI on retry");
        retriedRequest.Headers.TryGetValues("X-Api-Key", out IEnumerable<string>? keyValues).ShouldBeTrue(
            "SonarrDelegatingHandler should have stamped X-Api-Key on retry");
        keyValues.ShouldNotBeNull();
        keyValues.ShouldContain(SonarrApiKey);
    }

    private sealed class CapturingFakeHandler(Func<HttpRequestMessage, HttpStatusCode> nextStatus)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(nextStatus(request)));
    }
}
