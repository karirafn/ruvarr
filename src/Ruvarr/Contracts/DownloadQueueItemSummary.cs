namespace Ruvarr.Contracts;

public sealed record DownloadQueueItemSummary(
    string EpisodeRuvId,
    string EpisodeTitle,
    string ProgramName,
    DateTime Created,
    DateTime? Downloaded,
    DownloadQueueStatus Status);