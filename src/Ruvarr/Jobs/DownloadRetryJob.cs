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

        List<DownloadQueueItem> due = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Failed)
            .Where(x => x.NextRetryAt != null && x.NextRetryAt <= utcNow)
            .ToListAsync(cancellationToken);

        foreach (DownloadQueueItem item in due)
        {
            item.RequeueForRetry();
        }

        if (due.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Requeued {Count} download queue items for retry", due.Count);
        }
    }
}
