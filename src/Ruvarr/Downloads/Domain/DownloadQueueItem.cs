using Ruvarr.Ruv.Domain;

namespace Ruvarr.Downloads.Domain;

internal sealed class DownloadQueueItem
{
    private DownloadQueueItem()
    {
    }

    public required RuvEpisode Episode { get; init; }

    public required DateTime Created { get; init; }

    public static DownloadQueueItem Create(RuvEpisode episode) => new()
    {
        Episode = episode,
        Created = DateTime.UtcNow,
    };
}