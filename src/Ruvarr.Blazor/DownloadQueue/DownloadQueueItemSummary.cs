namespace Ruvarr.Blazor.DownloadQueue;

internal sealed record DownloadQueueItemSummary(
    string EpisodeRuvId,
    string EpisodeTitle,
    string ProgramName,
    DateTime Created,
    DateTime? Downloaded);
