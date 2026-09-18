// Tests the shared resilience pipeline behaviour (retry cap, Retry-After, attempt timeout)
// by driving the RÚV DI registration — representative of the AddRuvarrResilience helper —
// with a scripted primary handler substituted via ConfigurePrimaryHttpMessageHandler.
// No Docker or database is required.

using System.Diagnostics;
using System.Net;

using Microsoft.Extensions.Http.Resilience;

using Ruvarr.Infrastructure.Ruv;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Resilience;

public sealed class ResiliencePipelineBehaviorTests
{
    private const string ClientName = nameof(IRuvClient);

    // AddStandardResilienceHandler registers options under "{clientName}-standard".
    // Configure must target this key to override AttemptTimeout, Retry.Delay, etc.
    private const string ResilienceOptionsName = $"{ClientName}-standard";
    private const string BaseAddress = "https://api.ruv.is/";

    // Post-configure options common to all tests: sub-second attempt timeout and retry delay
    // so the suite stays fast. Invariants preserved: Total >= Attempt and
    // SamplingDuration >= 2 * Attempt.
    private static void ConfigureFastOptions(IServiceCollection services)
    {
        services.Configure<HttpStandardResilienceOptions>(ResilienceOptionsName, options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(300);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMilliseconds(700);
        });
    }

    private static ServiceProvider BuildProvider(
        ScriptedHttpMessageHandler handler,
        Action<IServiceCollection>? configure = null)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ruv:BaseAddress"] = BaseAddress,
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        ConfigureFastOptions(services);
        configure?.Invoke(services);

        services.AddHttpClient<IRuvClient, RuvClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    // TDD cycle (b) — Retry cap (AC #3):
    // An all-503 script exhausts retries: the standard handler allows 3 retries for a total
    // of 4 attempts, then returns the last failure response.
    [Fact]
    public async Task WhenAllAttemptsReturn503_ExactlyFourAttemptsAreMadeAndFailureSurfaces()
    {
        // Arrange — script repeats 503 indefinitely; the pipeline must cap at 4 total.
        using ScriptedHttpMessageHandler handler = new(new ResponseSpec(HttpStatusCode.ServiceUnavailable));

        await using ServiceProvider provider = BuildProvider(handler);
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(ClientName);

        // Act — the pipeline exhausts retries and returns the last 503 response.
        using HttpResponseMessage response = await client.GetAsync(
            $"{BaseAddress}api/programs/program/1234/all",
            CancellationToken.None);

        // Assert — the final response is 503 and exactly 1 original + 3 retries = 4 total attempts.
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable,
            "the pipeline returns the last failure response after exhausting retries");
        handler.RequestCount.ShouldBe(4, "standard handler allows 3 retries (4 total attempts) on transient 503");
    }

    // TDD cycle (c) — Retry-After honoured (AC #4):
    // A [429 + Retry-After: 1s, 200] script must produce a total elapsed time substantially
    // longer than an immediate retry (lower bound only — never an upper bound or exact timing),
    // and the overall call must succeed in 2 attempts.
    [Fact]
    public async Task WhenFirstAttemptReturns429WithRetryAfterHeader_DelaysAtLeastHeaderDurationThenSucceeds()
    {
        // Retry-After is 1s. The lower-bound assertion uses 900ms to absorb timer resolution
        // variance (~10ms) and pipeline scheduling overhead, while still proving the header is
        // honoured — an uninhibited retry would complete in < 50ms, so any assertion above 100ms
        // would distinguish the two cases. Never assert an upper bound or exact time.
        TimeSpan retryAfterDuration = TimeSpan.FromSeconds(1);
        TimeSpan assertionLowerBound = TimeSpan.FromMilliseconds(900);

        // Arrange — first response carries Retry-After: 1s so the pipeline waits ≥ ~900ms.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.TooManyRequests, RetryAfter: retryAfterDuration),
            new ResponseSpec(HttpStatusCode.OK));

        // Override TotalRequestTimeout to accommodate the 1s Retry-After + margins.
        ServiceCollection services = [];
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ruv:BaseAddress"] = BaseAddress,
            })
            .Build();
        services.AddSingleton(configuration);
        services.AddRuv();

        // Use a longer total timeout to fit the Retry-After + margins.
        // Target the "-standard" name under which AddStandardResilienceHandler registers options.
        services.Configure<HttpStandardResilienceOptions>(ResilienceOptionsName, options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient<IRuvClient, RuvClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(ClientName);

        // Act
        Stopwatch stopwatch = Stopwatch.StartNew();
        using HttpResponseMessage response = await client.GetAsync(
            $"{BaseAddress}api/programs/program/1234/all",
            CancellationToken.None);
        stopwatch.Stop();

        // Assert — call succeeded and the delay was at least the Retry-After value.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2, "pipeline retried once after the 429 + Retry-After");
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(assertionLowerBound,
            "pipeline must honour the Retry-After header and wait substantially longer than an immediate retry (~50ms); 900ms lower bound absorbs timer variance");
    }

    // TDD cycle (d) — Slow response triggers attempt timeout then retry (AC #1 timeout path):
    // A script where the first response is delayed beyond AttemptTimeout causes the pipeline to
    // abort that attempt, retry, and the second fast attempt succeeds.
    [Fact]
    public async Task WhenFirstAttemptExceedsAttemptTimeout_PipelineRetriesAndSucceedsOnSecondAttempt()
    {
        // Arrange — first spec delays longer than AttemptTimeout (300ms); second is immediate 200.
        // The delay uses a long value so it definitely exceeds the 300ms limit. The pipeline
        // cancels the first attempt via attempt-timeout and the handler propagates cancellation
        // rather than returning, so the pipeline retries.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.OK, Delay: TimeSpan.FromSeconds(2)),
            new ResponseSpec(HttpStatusCode.OK));

        await using ServiceProvider provider = BuildProvider(handler);
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(ClientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            $"{BaseAddress}api/programs/program/1234/all",
            CancellationToken.None);

        // Assert — the call ultimately succeeds (second attempt returns 200 immediately).
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2,
            "first attempt should time out via AttemptTimeout; pipeline retries and second attempt succeeds");
    }
}
