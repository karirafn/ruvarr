using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class EpisodeAddedToMatchedProgramEventHandler(
    TvdbEpisodeLookupNotifier notifier,
    IDomainEventBroadcaster broadcaster) : IDomainEventHandler<EpisodeAddedToMatchedProgramEvent>
{
    public Task Handle(EpisodeAddedToMatchedProgramEvent @event, CancellationToken cancellationToken)
    {
        notifier.Enqueue(@event.RuvId, @event.Name);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
        return Task.CompletedTask;
    }
}
