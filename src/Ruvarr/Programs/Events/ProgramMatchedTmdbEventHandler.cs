using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramMatchedTmdbEventHandler(IDomainEventBroadcaster broadcaster) : IDomainEventHandler<ProgramMatchedTmdbEvent>
{
    public Task Handle(ProgramMatchedTmdbEvent @event, CancellationToken cancellationToken)
    {
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
