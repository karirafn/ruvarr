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

        List<string> fileNames = orphans
            .Select(x => x.FileName)
            .OfType<string>()
            .ToList();

        if (fileNames.Count < orphans.Count)
        {
            logger.LogDebug(
                "Skipping {Count} orphan(s) with null FileName (pre-migration rows)",
                orphans.Count - fileNames.Count);
        }

        foreach (string fileName in fileNames)
        {
            logger.LogInformation("Deleting incomplete file {FileName} left by crashed process", fileName);
            fileStore.DeleteIncomplete(settings, fileName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
