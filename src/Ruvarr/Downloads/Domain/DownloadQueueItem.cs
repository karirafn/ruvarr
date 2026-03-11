using Ruvarr.Ruv.Domain;

namespace Ruvarr.Downloads.Domain;

internal sealed class DownloadQueueItem
{
    private DownloadQueueItem()
    {
    }

    public required RuvEpisode Episode { get; init; }

    public required DateTime Created { get; init; }

    public DateTime? Downloaded { get; private set; }

    public DownloadQueueStatus Status { get; private set; } = DownloadQueueStatus.Pending;

    public static DownloadQueueItem Create(RuvEpisode episode) => new()
    {
        Episode = episode,
        Created = DateTime.UtcNow,
    };

    public void MarkDownloading() => Status = DownloadQueueStatus.Downloading;

    public void MarkDownloaded()
    {
        Downloaded = DateTime.UtcNow;
        Status = DownloadQueueStatus.Complete;
    }
}