using Ruvarr.Contracts;

namespace Ruvarr.Downloads.Queries.GetDownloadQueue;

internal sealed record DownloadQueueItemSummary(
    string EpisodeRuvId,
    int ProgramRuvId,
    string ProgramName,
    string EpisodeTitle,
    DownloadQueueStatus Status,
    string? FailureReason,
    int RetryCount,
    DateTime? NextRetryAt,
    DateTime Created);
