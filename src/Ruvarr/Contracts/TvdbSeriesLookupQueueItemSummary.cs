namespace Ruvarr.Contracts;

public sealed record TvdbSeriesLookupQueueItemSummary(
    int RuvId,
    string ProgramName,
    TvdbSeriesLookupStatus Status) : IQueueItemSummary;