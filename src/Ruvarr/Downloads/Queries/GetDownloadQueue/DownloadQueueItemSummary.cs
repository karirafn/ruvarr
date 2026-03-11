namespace Ruvarr.Downloads.Queries.GetDownloadQueue;

public sealed record DownloadQueueItemSummary(
    string EpisodeRuvId,
    string EpisodeTitle,
    string ProgramName,
    DateTime Created,
    DateTime? Downloaded);
