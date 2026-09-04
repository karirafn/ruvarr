using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Testing.Builders;

internal sealed class DownloadQueueItemBuilder
{
    private RuvEpisode _episode = new RuvEpisodeBuilder().Build();
    private string _failedReason = "test reason";
    private ItemState _state = ItemState.Pending;

    private enum ItemState
    {
        Pending,
        Failed,
        FailedAndDue,
        Exhausted,
        Downloaded,
    }

    public DownloadQueueItemBuilder WithEpisode(RuvEpisode episode)
    {
        _episode = episode;
        return this;
    }

    /// <summary>
    /// Configures the builder to produce a Failed item with the given reason (RetryCount = 1,
    /// NextRetryAt in the future — not yet due for automatic retry).
    /// </summary>
    public DownloadQueueItemBuilder Failed(string reason)
    {
        _failedReason = reason;
        _state = ItemState.Failed;
        return this;
    }

    /// <summary>
    /// Configures the builder to produce a Failed item that is already due for retry.
    /// NextRetryAt is backdated to the past.
    /// </summary>
    public DownloadQueueItemBuilder FailedAndDue(string reason)
    {
        _failedReason = reason;
        _state = ItemState.FailedAndDue;
        return this;
    }

    /// <summary>
    /// Configures the builder to produce an Exhausted item
    /// (RetryCount = MaxRetries + 1, NextRetryAt = null).
    /// </summary>
    public DownloadQueueItemBuilder Exhausted()
    {
        _state = ItemState.Exhausted;
        return this;
    }

    /// <summary>
    /// Configures the builder to produce a Complete item.
    /// </summary>
    public DownloadQueueItemBuilder Downloaded()
    {
        _state = ItemState.Downloaded;
        return this;
    }

    public DownloadQueueItem Build()
    {
        DownloadQueueItem item = DownloadQueueItem.Create(_episode);

        switch (_state)
        {
            case ItemState.Failed:
                item.MarkDownloading();
                item.MarkFailed(_failedReason);
                break;

            case ItemState.FailedAndDue:
                item.MarkDownloading();
                item.MarkFailed(_failedReason);
                item.BackdateNextRetryAt();
                break;

            case ItemState.Exhausted:
                item.MarkDownloading();
                for (int i = 0; i <= RetrySchedule.MaxRetries; i++)
                {
                    item.MarkFailed("exhaustion failure");
                }
                break;

            case ItemState.Downloaded:
                item.MarkDownloading();
                item.MarkDownloaded();
                break;
        }

        return item;
    }
}
