namespace Ruvarr.Contracts;

public sealed record ProgramRefreshQueueItemSummary(
    int RuvId,
    string ProgramName,
    ProgramRefreshStatus Status) : IQueueItemSummary;