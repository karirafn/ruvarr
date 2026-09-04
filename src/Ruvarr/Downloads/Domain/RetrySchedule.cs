namespace Ruvarr.Downloads.Domain;

internal static class RetrySchedule
{
    internal const int MaxRetries = 5;

    internal static DateTime ComputeNextRetry(int retryCount) => DateTime.UtcNow.Add(retryCount switch
    {
        1 => TimeSpan.FromHours(1),
        2 => TimeSpan.FromHours(2),
        3 => TimeSpan.FromHours(4),
        4 => TimeSpan.FromDays(1),
        _ => TimeSpan.FromDays(7),
    });
}
