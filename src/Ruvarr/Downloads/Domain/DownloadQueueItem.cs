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

    public void MarkFailed()
    {
        Status = DownloadQueueStatus.Failed;
        _domainEvents.Add(new DownloadFailedEvent(this));
    }
}