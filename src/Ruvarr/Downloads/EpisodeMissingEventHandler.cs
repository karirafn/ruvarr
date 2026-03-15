using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal sealed class EpisodeMissingEventHandler(RuvarrDbContext dbContext) : IDomainEventHandler<EpisodeMissingEvent>
{
    public Task Handle(EpisodeMissingEvent @event, CancellationToken cancellationToken)
    {
        RuvEpisode episode = @event.Episode;

        // Check the shadow FK property so we detect an existing queue item
        // even when the DownloadQueueItem navigation property isn't loaded.
        int? downloadQueueItemId = (int?)dbContext.Entry(episode).Property("download_queue_item_id").CurrentValue;
        if (downloadQueueItemId == null)
        {
            dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        }

        return Task.CompletedTask;
    }
}