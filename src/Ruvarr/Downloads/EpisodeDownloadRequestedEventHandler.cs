using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal sealed class EpisodeDownloadRequestedEventHandler(RuvarrDbContext dbContext) : IDomainEventHandler<EpisodeDownloadRequestedEvent>
{
    public Task Handle(EpisodeDownloadRequestedEvent @event, CancellationToken cancellationToken)
    {
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(@event.Episode));
        return Task.CompletedTask;
    }
}
