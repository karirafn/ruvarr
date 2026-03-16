using Ruvarr.Abstractions;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramMatchedTvdbEventHandler(TvdbEpisodeLookupNotifier notifier) : IDomainEventHandler<ProgramMatchedTvdbEvent>
{
    public Task Handle(ProgramMatchedTvdbEvent @event, CancellationToken cancellationToken)
    {
        notifier.Enqueue(@event.RuvId, @event.Name);
        return Task.CompletedTask;
    }
}
