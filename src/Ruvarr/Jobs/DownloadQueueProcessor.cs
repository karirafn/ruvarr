using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadQueueProcessor(
    ILogger<DownloadQueueProcessor> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IRuvStreamInspector streamInspector,
    ISettingsStore settingsStore,
    DownloadProgressNotifier progressNotifier,
    DownloadFileStore fileStore) : IJob
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

        // Outcome writes must land even when the scheduler is shutting down — a cancelled save
        // leaves the item stuck in Downloading with no record of what happened to it.
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
            item.MarkFailed();
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

        string completedPath;
        try
        {
            completedPath = DownloadFileStore.MoveToCompleted(settings, fileName);
        }
#pragma warning disable CA1031 // Move failure must not leave item stuck in Downloading state
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Failed to move {FileName} to completed directory for {Episode}", fileName, item.Episode.ToString());
            item.MarkFailed();
            await dbContext.SaveChangesAsync(outcomeWrite);
            return;
        }

        item.MarkDownloaded();
        progressNotifier.CompleteDownload();

        await dbContext.SaveChangesAsync(outcomeWrite);

        if (item.Episode.TvdbEpisodes.Count == 0)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        try
        {
            IReadOnlyList<Series> sonarrSeries = await sonarr.GetSeriesAsync(cancellationToken);
            int? sonarrSeriesId = sonarrSeries
                .FirstOrDefault(s => s.TvdbId == item.Episode.Program.Series?.TvdbId)
                ?.Id;

            IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(
                settings.ResolvedEpisodeDownloadDirectory,
                seriesId: null,
                cancellationToken);

            ManualImportFile? file = manualImportFiles
                .FirstOrDefault(x => x.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (file is null)
            {
                logger.LogWarning(
                    "Sonarr scan of {Folder} did not include {Filename}. Marking {Episode} failed",
                    settings.ResolvedEpisodeDownloadDirectory, fileName, item.Episode.ToString());
                item.MarkFailed();
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            int? resolvedSeriesId = sonarrSeriesId ?? file.Series?.Id;
            if (resolvedSeriesId is null)
            {
                logger.LogWarning(
                    "Sonarr has no series for {Episode} (no TVDB-id match and no scan-matched series). Marking failed",
                    item.Episode.ToString());
                item.MarkFailed();
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            IReadOnlyList<SonarrEpisode> sonarrEpisodes = await sonarr.GetEpisodesAsync(
                resolvedSeriesId.Value, cancellationToken);

            Dictionary<int, int> tvdbIdToSonarrEpisodeId = [];
            foreach (SonarrEpisode sonarrEpisode in sonarrEpisodes)
            {
                if (sonarrEpisode.TvdbId != 0)
                {
                    tvdbIdToSonarrEpisodeId.TryAdd(sonarrEpisode.TvdbId, sonarrEpisode.Id);
                }
            }

            if (!item.Episode.TryResolveSonarrEpisodeIds(tvdbIdToSonarrEpisodeId, out IReadOnlyList<int> episodeIds))
            {
                IEnumerable<int> unresolved = item.Episode.TvdbEpisodes
                    .Select(e => e.TvdbId)
                    .Where(id => !tvdbIdToSonarrEpisodeId.ContainsKey(id));
                logger.LogWarning(
                    "Sonarr series {SeriesId} is missing episodes for TVDB ids {UnresolvedTvdbIds}. Marking {Episode} failed",
                    resolvedSeriesId.Value, string.Join(", ", unresolved), item.Episode.ToString());
                item.MarkFailed();
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            ManualImportRequest request = new(
                Path: completedPath,
                SeriesId: resolvedSeriesId.Value,
                EpisodeIds: episodeIds,
                Quality: file.Quality,
                Languages: file.Languages,
                ReleaseGroup: "RUV");

            logger.LogInformation("Importing {Episode} into Sonarr", item.Episode.ToString());
            await sonarr.ManualImportFilesAsync([request], cancellationToken);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent download items from getting stuck in Downloading state
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Sonarr import failed for {Episode}", item.Episode.ToString());
            item.MarkFailed();
            await dbContext.SaveChangesAsync(outcomeWrite);
        }
    }
}
