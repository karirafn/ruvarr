using Ruvarr.Abstractions;

namespace Ruvarr.Downloads.Events;

internal sealed class DownloadCompletedEventHandler(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<DownloadCompletedEvent>
{
    public Task Handle(DownloadCompletedEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
