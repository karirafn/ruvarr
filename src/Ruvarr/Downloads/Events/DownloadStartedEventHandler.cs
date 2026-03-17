using Ruvarr.Abstractions;

namespace Ruvarr.Downloads.Events;

internal sealed class DownloadStartedEventHandler(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<DownloadStartedEvent>
{
    public Task Handle(DownloadStartedEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
