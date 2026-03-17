using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramCreatedEventHandler(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<ProgramCreatedEvent>
{
    public Task Handle(ProgramCreatedEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
