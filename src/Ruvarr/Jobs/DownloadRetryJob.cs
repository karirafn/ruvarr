using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadRetryJob(RuvarrDbContext dbContext) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        DateTime utcNow = DateTime.UtcNow;

        List<DownloadQueueItem> due = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Failed)
            .Where(x => x.NextRetryAt != null && x.NextRetryAt <= utcNow)
            .ToListAsync(context.CancellationToken);

        foreach (DownloadQueueItem item in due)
        {
            item.RequeueForRetry();
        }

        if (due.Count > 0)
        {
            await dbContext.SaveChangesAsync(context.CancellationToken);
        }
    }
}
