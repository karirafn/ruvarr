using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal sealed class EpisodeDownloadRequestedEventHandler(RuvarrDbContext dbContext) : IDomainEventHandler<EpisodeDownloadRequestedEvent>
{
    public async Task Handle(EpisodeDownloadRequestedEvent @event, CancellationToken cancellationToken)
    {
        int episodeId = (int)dbContext.Entry(@event.Episode).Property("id").CurrentValue!;

        DownloadQueueItem? unfinished = await dbContext.Set<DownloadQueueItem>()
            .Where(x => EF.Property<int?>(x, "episode_id") == episodeId)
            .Where(x => x.Status != DownloadQueueStatus.Complete)
            .SingleOrDefaultAsync(cancellationToken);

        if (unfinished is null)
        {
            dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(@event.Episode));
        }
        else if (unfinished.CanRetry)
        {
            // Reusing the item keeps its RetryCount, so a manual download cannot bypass the retry limit.
            unfinished.RetryNow();
        }
    }
}
