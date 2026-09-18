namespace Ruvarr.Abstractions;

internal static class ResilienceHttpClientBuilderExtensions
{
    // Attaches the standard resilience pipeline (retry 3× exponential+jitter over
    // 5xx/408/429/timeout, circuit breaker, Retry-After) to the builder with timeout
    // values from ApiClientTimeouts. HttpClient.Timeout must be set to
    // Timeout.InfiniteTimeSpan on clients using this helper so the pipeline is the
    // sole timeout authority (see ADR 0010).
    internal static IHttpClientBuilder AddRuvarrResilience(this IHttpClientBuilder builder)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = ApiClientTimeouts.TotalPipeline;
            options.AttemptTimeout.Timeout = ApiClientTimeouts.PerAttempt;
        });

        return builder;
    }
}
