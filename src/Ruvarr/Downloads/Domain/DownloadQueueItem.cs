using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Events;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Downloads.Domain;

internal sealed class DownloadQueueItem
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private DownloadQueueItem()
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public required RuvEpisode Episode { get; init; }

    public required DateTime Created { get; init; }

    public DateTime? Downloaded { get; private set; }

    public string? FileName { get; private set; }

    public DownloadQueueStatus Status { get; private set; } = DownloadQueueStatus.Pending;

    public int RetryCount { get; private set; }

    public DateTime? NextRetryAt { get; private set; }

    public string? FailureReason { get; private set; }

    public string? CompletedFileName => Status is DownloadQueueStatus.Failed or DownloadQueueStatus.Exhausted
        ? FileName
        : null;

    public static DownloadQueueItem Create(RuvEpisode episode) => new()
    {
        Episode = episode,
        Created = DateTime.UtcNow,
    };

    public void MarkDownloading()
    {
        FileName = Episode.ToFilename();
        Status = DownloadQueueStatus.Downloading;
        _domainEvents.Add(new DownloadStartedEvent(this));
    }

    public void MarkDownloaded()
    {
        Downloaded = DateTime.UtcNow;
        Status = DownloadQueueStatus.Complete;
        _domainEvents.Add(new DownloadCompletedEvent(this));
    }

    public void MarkFailed(string reason)
    {
        FailureReason = reason;
        RetryCount++;

        if (RetryCount <= RetrySchedule.MaxRetries)
        {
            Status = DownloadQueueStatus.Failed;
            NextRetryAt = RetrySchedule.ComputeNextRetry(RetryCount);
        }
        else
        {
            Status = DownloadQueueStatus.Exhausted;
            NextRetryAt = null;
        }

        _domainEvents.Add(new DownloadFailedEvent(this));
    }

    public void RequeueForRetry()
    {
        if (Status is not DownloadQueueStatus.Failed)
        {
            throw new InvalidOperationException("Only Failed items can be automatically requeued for retry.");
        }

        if (NextRetryAt is null || NextRetryAt > DateTime.UtcNow)
        {
            throw new InvalidOperationException("Item is not yet due for retry.");
        }

        Status = DownloadQueueStatus.Pending;
        NextRetryAt = null;
        _domainEvents.Add(new DownloadRetryScheduledEvent(this));
    }

    public void RetryNow()
    {
        if (Status is not DownloadQueueStatus.Failed and not DownloadQueueStatus.Exhausted)
        {
            throw new InvalidOperationException("Only Failed or Exhausted items can be manually retried.");
        }

        Status = DownloadQueueStatus.Pending;
        NextRetryAt = null;
        FailureReason = null;
        _domainEvents.Add(new DownloadRetryScheduledEvent(this));
    }

    // Exposed internal for test builders to simulate time passing (NextRetryAt becoming due).
    // Production code never calls this — only DownloadQueueItemBuilder.FailedAndDue() does.
    internal void BackdateNextRetryAt()
    {
        NextRetryAt = DateTime.UtcNow.AddHours(-1);
    }
}