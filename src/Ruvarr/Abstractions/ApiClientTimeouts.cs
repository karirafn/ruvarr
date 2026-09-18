namespace Ruvarr.Abstractions;

internal static class ApiClientTimeouts
{
    // Total budget across all retry attempts. Matches the prior HttpClient.Timeout (30s)
    // so migrating clients to the resilience pipeline preserves the existing call budget.
    internal static readonly TimeSpan TotalPipeline = TimeSpan.FromSeconds(30);

    // Per-attempt ceiling. Must satisfy: TotalPipeline >= PerAttempt and
    // CircuitBreaker.SamplingDuration (default 30s) >= 2 * PerAttempt.
    internal static readonly TimeSpan PerAttempt = TimeSpan.FromSeconds(10);

    // Caps the delay from Retry-After headers and exponential backoff. TotalRequestTimeout
    // is the hard outer limit; this adds defense-in-depth so a malicious or misconfigured
    // Retry-After: 86400 cannot freeze a retry slot for the full budget.
    internal static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(15);
}
