using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadRetryJob(
    ILogger<DownloadRetryJob> logger,
    RuvarrDbContext dbContext) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Starting download retry job");

        DateTime utcNow = DateTime.UtcNow;

        List<DownloadQueueItem> candidates = await dbContext.Set<DownloadQueueItem>()
            .Include(x => x.Episode)
                .ThenInclude(x => x.TvdbEpisodes)
            .Where(x => x.Status == DownloadQueueStatus.Failed || x.Status == DownloadQueueStatus.Exhausted)
            .ToListAsync(cancellationToken);

        int dismissed = 0;
        int requeued = 0;

        foreach (DownloadQueueItem item in candidates)
        {
            // Superseded wins over a due retry: re-queueing would import a duplicate of what Sonarr already has.
            if (item.IsSuperseded)
            {
                item.Dismiss();
                dbContext.Set<DownloadQueueItem>().Remove(item);
                dismissed++;
            }
            else if (item.Status == DownloadQueueStatus.Failed && item.NextRetryAt <= utcNow)
            {
                item.RequeueForRetry();
                requeued++;
            }
        }

        if (dismissed + requeued == 0)
        {
            return;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (dismissed > 0)
        {
            logger.LogInformation("Dismissed {Count} superseded download queue items", dismissed);
        }

        if (requeued > 0)
        {
            logger.LogInformation("Requeued {Count} download queue items for retry", requeued);
        }
    }
}
