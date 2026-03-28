namespace Ruvarr.Abstractions;

internal sealed class BroadcastEventHandler<TEvent>(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<TEvent>
    where TEvent : class, IDomainEvent
{
    public Task Handle(TEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
