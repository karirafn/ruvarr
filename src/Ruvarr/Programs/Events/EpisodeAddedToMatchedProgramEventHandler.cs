using Ruvarr.Abstractions;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class EpisodeAddedToMatchedProgramEventHandler(TvdbEpisodeLookupNotifier notifier) : IDomainEventHandler<EpisodeAddedToMatchedProgramEvent>
{
    public Task Handle(EpisodeAddedToMatchedProgramEvent @event, CancellationToken cancellationToken)
    {
        notifier.Enqueue(@event.RuvId, @event.Name);
        return Task.CompletedTask;
    }
}
