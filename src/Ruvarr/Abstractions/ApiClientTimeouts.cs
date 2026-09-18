namespace Ruvarr.Abstractions;

internal static class ApiClientTimeouts
{
    // Measured healthy latency is ~2.1s; 30s is approximately 14x headroom.
    internal static readonly TimeSpan Default = TimeSpan.FromSeconds(30);

    // Total budget across all retry attempts. Matches Default so migrating
    // clients from HttpClient.Timeout to the resilience pipeline preserves the
    // existing 30s call budget.
    internal static readonly TimeSpan TotalPipeline = TimeSpan.FromSeconds(30);

    // Per-attempt ceiling. Must satisfy: TotalPipeline >= PerAttempt and
    // CircuitBreaker.SamplingDuration (default 30s) >= 2 * PerAttempt.
    internal static readonly TimeSpan PerAttempt = TimeSpan.FromSeconds(10);
}
