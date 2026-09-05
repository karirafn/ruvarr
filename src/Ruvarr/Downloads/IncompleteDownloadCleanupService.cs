using Microsoft.EntityFrameworkCore;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Settings;

namespace Ruvarr.Downloads;

internal sealed class IncompleteDownloadCleanupService(
    IServiceScopeFactory serviceScopeFactory,
    DownloadFileStore fileStore,
    ISettingsStore settingsStore,
    ILogger<IncompleteDownloadCleanupService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Scanning for incomplete downloads left by a crashed prior process");

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        List<DownloadQueueItem> orphans = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Downloading)
            .ToListAsync(cancellationToken);

        RuvarrSettings settings = settingsStore.Current;

        int nullFileNameCount = orphans.Count(x => x.FileName is null);
        if (nullFileNameCount > 0)
        {
            logger.LogDebug(
                "Skipping file deletion for {Count} orphan(s) with null FileName (pre-migration rows)",
                nullFileNameCount);
        }

        foreach (DownloadQueueItem orphan in orphans)
        {
            if (orphan.FileName is not null)
            {
                logger.LogInformation("Deleting incomplete file {FileName} left by crashed process", orphan.FileName);
                fileStore.DeleteIncomplete(settings, orphan.FileName);
            }

            orphan.MarkInterrupted();
        }

        // Use CancellationToken.None so the reclamation write lands even if the host's startup
        // token is cancelled mid-save on a fast shutdown — mirrors DownloadQueueProcessor's
        // outcomeWrite = CancellationToken.None pattern.
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
