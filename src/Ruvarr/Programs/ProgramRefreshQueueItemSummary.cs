namespace Ruvarr.Programs;

public sealed record ProgramRefreshQueueItemSummary(
    int RuvId,
    string ProgramName,
    ProgramRefreshStatus Status);
