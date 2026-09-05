using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadQueueProcessor(
    ILogger<DownloadQueueProcessor> logger,
    RuvarrDbContext dbContext,
    IFfmpegService ffmpeg,
    IRuvStreamInspector streamInspector,
    ISettingsStore settingsStore,
    DownloadProgressNotifier progressNotifier,
    DownloadFileStore fileStore,
    SonarrImporter sonarrImporter) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!settingsStore.Current.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping {JobName}: Sonarr is not configured", nameof(DownloadQueueProcessor));
            return;
        }

        logger.LogDebug("Starting download queue processor job");

        CancellationToken cancellationToken = context.CancellationToken;

        // The outcomeWrite token is CancellationToken.None so that genuine ffmpeg/move failure
        // writes succeed even when the scheduler is concurrently shutting down — a cancelled save
        // would leave the failure unrecorded. Interruption (cancellation with no failure) takes a
        // different path: the OCE propagates out of Execute() unhandled, the catch-alls carry
        // when (ex is not OperationCanceledException) and do not match, and the item is left
        // Downloading. IncompleteDownloadCleanupService reclaims it as Pending on next startup.
        CancellationToken outcomeWrite = CancellationToken.None;

        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Include(x => x.Episode)
                .ThenInclude(x => x.Program)
            .Include(x => x.Episode)
                .ThenInclude(x => x.TvdbEpisodes)
            .Where(x => x.Status == DownloadQueueStatus.Pending)
            .OrderBy(x => x.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            logger.LogDebug("No pending items in download queue");
            return;
        }

        item.MarkDownloading();
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvarrSettings settings = settingsStore.Current;
        string fileName = item.FileName!;
        string completedPath = DownloadFileStore.CompletedPath(settings, fileName);
        bool reuseExisting = DownloadFileStore.CompletedFileExists(settings, fileName);

        if (!reuseExisting)
        {
            string incompletePath = DownloadFileStore.IncompletePath(settings, fileName);
            DownloadFileStore.EnsureIncompleteDirectory(settings);

            logger.LogInformation("Downloading {Episode}", item.Episode.ToString());

            StreamSizeEstimate? estimate = await streamInspector.EstimateStreamSizeAsync(
                item.Episode.Uri,
                cancellationToken);

            string? seasonEpisodeLabel = item.Episode.SeasonEpisodeLabel;
            progressNotifier.StartDownload(
                item.Episode.Program.Name,
                item.Episode.Title,
                seasonEpisodeLabel,
                totalSize: estimate?.EstimatedBytes);

            Progress<FfmpegProgressData> progress = new(data => progressNotifier.ReportProgress(data));

            try
            {
                await ffmpeg.DownloadAsync(item.Episode.Uri, incompletePath, item.Episode.Title, progress, cancellationToken);
            }
#pragma warning disable CA1031 // Catch all exceptions to prevent download items from getting stuck in Downloading state
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "FFmpeg download failed for {Episode}", item.Episode.ToString());
                fileStore.DeleteIncomplete(settings, fileName);
                item.MarkFailed("FFmpeg download failed");
                progressNotifier.FailDownload();
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            try
            {
                TimeSpan? trimPoint = await ffmpeg.DetectTrimPointAsync(incompletePath, cancellationToken);
                if (trimPoint is not null)
                {
                    logger.LogInformation("Trimming {TrimPoint:g} from start of {Episode}", trimPoint, item.Episode.ToString());
                    await ffmpeg.TrimStartAsync(incompletePath, trimPoint.Value, cancellationToken);
                }
            }
#pragma warning disable CA1031 // Trim failures should not fail the download
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogWarning(ex, "Trim detection/execution failed for {Episode}. Continuing with untrimmed file", item.Episode.ToString());
            }

            try
            {
                completedPath = DownloadFileStore.MoveToCompleted(settings, fileName);
            }
#pragma warning disable CA1031 // Move failure must not leave item stuck in Downloading state
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Failed to move {FileName} to completed directory for {Episode}", fileName, item.Episode.ToString());
                item.MarkFailed("Failed to move file to completed directory");
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }
        }
        else
        {
            logger.LogInformation("Reusing existing completed file for {Episode}", item.Episode.ToString());
        }

        item.MarkDownloaded();
        progressNotifier.CompleteDownload();

        await dbContext.SaveChangesAsync(outcomeWrite);

        await sonarrImporter.ImportAsync(item, settings, fileName, completedPath, cancellationToken);
    }
}
