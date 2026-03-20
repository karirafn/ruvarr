using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramMatchedTvdbEventHandler(
    TvdbEpisodeLookupNotifier notifier,
    IDomainEventBroadcaster broadcaster) : IDomainEventHandler<ProgramMatchedTvdbEvent>
{
    public Task Handle(ProgramMatchedTvdbEvent @event, CancellationToken cancellationToken)
    {
        notifier.Enqueue(@event.RuvId, @event.Name);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
        broadcaster.Publish(@event);
        return Task.CompletedTask;
    }
}
