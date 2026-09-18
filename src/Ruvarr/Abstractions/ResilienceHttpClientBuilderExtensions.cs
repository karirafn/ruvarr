using Microsoft.Extensions.Http.Resilience;

namespace Ruvarr.Abstractions;

internal static class ResilienceHttpClientBuilderExtensions
{
    /// <summary>
    /// Attaches the standard resilience pipeline (retry 3× exponential+jitter over
    /// 5xx/408/429/timeout, circuit breaker, <c>Retry-After</c>) to the builder with
    /// timeout values from <see cref="ApiClientTimeouts"/>.
    /// </summary>
    /// <remarks>
    /// Callers must set <c>HttpClient.Timeout = Timeout.InfiniteTimeSpan</c> — the pipeline
    /// is the sole timeout authority (see ADR 0010). Omitting <c>Timeout.InfiniteTimeSpan</c>
    /// leaves a racing outer timeout that can abort a retry mid-flight.
    /// </remarks>
    internal static IHttpClientBuilder AddRuvarrResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = ApiClientTimeouts.TotalPipeline;
            options.AttemptTimeout.Timeout = ApiClientTimeouts.PerAttempt;

            // Only retry safe (idempotent) methods — GET, HEAD, OPTIONS, etc.
            // POST/PUT/PATCH/DELETE are excluded to prevent duplicate Sonarr commands
            // (ManualImportFilesAsync → POST api/v3/command, AddSeriesAsync → POST api/v3/series).
            options.Retry.DisableForUnsafeHttpMethods();

            // Cap the delay from Retry-After headers and exponential backoff as defense-in-depth.
            // TotalRequestTimeout is the hard outer limit; MaxDelay prevents a single large
            // Retry-After value from consuming the entire budget before any retry attempt.
            options.Retry.MaxDelay = ApiClientTimeouts.MaxRetryDelay;
        });

        return builder;
    }
}
