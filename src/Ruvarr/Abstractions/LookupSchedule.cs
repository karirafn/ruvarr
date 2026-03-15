namespace Ruvarr.Abstractions;

internal static class LookupSchedule
{
    internal static DateTime ComputeNextLookup(int lookupCount) => DateTime.UtcNow.Add(lookupCount switch
    {
        1 => TimeSpan.FromHours(1),
        2 => TimeSpan.FromHours(2),
        3 => TimeSpan.FromHours(4),
        4 => TimeSpan.FromDays(1),
        _ => TimeSpan.FromDays(7)
    });
}