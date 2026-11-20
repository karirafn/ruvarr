using Microsoft.EntityFrameworkCore;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.SonarrFolderCheck;

// Level-triggered: every Passing result re-queues whatever still carries the kind, so a restart that
// fixes the mount needs no memory of the earlier Failing result.
internal sealed class SonarrFolderCheckRequeuer(RuvarrDbContext dbContext, ILogger<SonarrFolderCheckRequeuer> logger)
{
    public async Task RequeueAsync(SonarrFolderCheckResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Status != SonarrFolderCheckStatus.Passing)
        {
            return;
        }

        List<DownloadQueueItem> candidates = await dbContext.Set<DownloadQueueItem>()
            .Include(x => x.Episode)
                .ThenInclude(x => x.TvdbEpisodes)
            .Where(x => x.Status == DownloadQueueStatus.Failed || x.Status == DownloadQueueStatus.Exhausted)
            .Where(x => x.FailureKind == DownloadFailureKind.SonarrCannotSeeDownloadFolder)
            .ToListAsync(cancellationToken);

        // Superseded items are left for DownloadRetryJob to dismiss; re-queueing them would import a duplicate.
        List<DownloadQueueItem> stranded = [.. candidates.Where(x => !x.IsSuperseded)];
        if (stranded.Count == 0)
        {
            return;
        }

        foreach (DownloadQueueItem item in stranded)
        {
            item.RetryNow();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Sonarr can see the download folder {Folder} again; re-queued {Count} downloads that failed because it could not",
            result.Folder, stranded.Count);
    }
}
