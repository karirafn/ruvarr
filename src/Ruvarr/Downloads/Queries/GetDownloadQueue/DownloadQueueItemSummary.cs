namespace Ruvarr.Downloads.Queries.GetDownloadQueue;

using Ruvarr.Downloads.Domain;

public sealed record DownloadQueueItemSummary(
    string EpisodeRuvId,
    string EpisodeTitle,
    string ProgramName,
    DateTime Created,
    DateTime? Downloaded,
    DownloadQueueStatus Status);
