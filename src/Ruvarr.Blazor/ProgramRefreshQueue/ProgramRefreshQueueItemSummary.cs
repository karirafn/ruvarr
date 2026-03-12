namespace Ruvarr.Blazor.ProgramRefreshQueue;

internal sealed record ProgramRefreshQueueItemSummary(
    int RuvId,
    string ProgramName,
    ProgramRefreshStatus Status);
