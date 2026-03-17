using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed class EpisodeMatchedEventHandler(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<EpisodeMatchedEvent>
{
    public Task Handle(EpisodeMatchedEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
