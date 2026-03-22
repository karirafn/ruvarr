using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal sealed class EpisodeMissingEventHandler(RuvarrDbContext dbContext) : IDomainEventHandler<EpisodeMissingEvent>
{
    public async Task Handle(EpisodeMissingEvent @event, CancellationToken cancellationToken)
    {
        int episodeId = (int)dbContext.Entry(@event.Episode).Property("id").CurrentValue!;

        bool hasActiveItem = await dbContext.Set<DownloadQueueItem>()
            .AnyAsync(x => EF.Property<int?>(x, "episode_id") == episodeId
                && x.Status != DownloadQueueStatus.Complete, cancellationToken);

        if (!hasActiveItem)
        {
            dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(@event.Episode));
        }
    }
}