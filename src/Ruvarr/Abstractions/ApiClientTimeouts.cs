namespace Ruvarr.Abstractions;

internal static class ApiClientTimeouts
{
    // Measured healthy latency is ~2.1s; 30s is approximately 14x headroom.
    internal static readonly TimeSpan Default = TimeSpan.FromSeconds(30);
}
