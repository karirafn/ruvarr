using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramRefreshRequestedEventHandler(
    ProgramRefreshNotifier programRefreshNotifier,
    TvdbSeriesLookupNotifier tvdbSeriesLookupNotifier,
    TvdbEpisodeLookupNotifier tvdbEpisodeLookupNotifier,
    IDomainEventBroadcaster broadcaster) : IDomainEventHandler<ProgramRefreshRequestedEvent>
{
    public Task Handle(ProgramRefreshRequestedEvent @event, CancellationToken cancellationToken)
    {
        programRefreshNotifier.PriorityEnqueue(@event.RuvId, @event.Name);
        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());

        if (!@event.HasSeries)
        {
            tvdbSeriesLookupNotifier.PriorityEnqueue(@event.RuvId, @event.Name);
            broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());
        }

        if (@event.HasSeries && @event.HasMultipleEpisodes)
        {
            tvdbEpisodeLookupNotifier.PriorityEnqueue(@event.RuvId, @event.Name);
            broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
        }

        return Task.CompletedTask;
    }
}
