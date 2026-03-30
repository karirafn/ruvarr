
using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal class DownloadQueueProcessor(
    ILogger<DownloadQueueProcessor> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IRuvStreamInspector streamInspector,
    ISettingsStore settingsStore,
    DownloadProgressNotifier progressNotifier) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!settingsStore.Current.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping {JobName}: Sonarr is not configured", nameof(DownloadQueueProcessor));
            return;
        }

        logger.LogDebug("Starting download queue processor job");

        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Include(x => x.Episode)
                .ThenInclude(x => x.Program)
            .Include(x => x.Episode)
                .ThenInclude(x => x.TvdbEpisodes)
            .Where(x => x.Status == DownloadQueueStatus.Pending)
            .OrderBy(x => x.Created)
            .FirstOrDefaultAsync();

        if (item is null)
        {
            logger.LogDebug("No pending items in download queue");
            return;
        }

        item.MarkDownloading();
        await dbContext.SaveChangesAsync();

        string episodeSubdirectory = settingsStore.Current.EpisodeDownloadDirectory;

        logger.LogInformation("Downloading {Episode}", item.Episode.ToString());

        string filepath;
        try
        {
            string downloadsRoot = settingsStore.Current.DownloadsRoot;

            string tentativePath = item.Episode.ToFilePath(
                downloadsRoot,
                episodeSubdirectory,
                fileAlreadyExists: false);

            filepath = item.Episode.ToFilePath(
                downloadsRoot,
                episodeSubdirectory,
                fileAlreadyExists: File.Exists(tentativePath));

            string? directory = Path.GetDirectoryName(filepath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Failed to resolve download path for {Episode}", item.Episode.ToString());
            item.MarkFailed();
            await dbContext.SaveChangesAsync();
            return;
        }

        string filename = Path.GetFileName(filepath);

        StreamSizeEstimate? estimate = await streamInspector.EstimateStreamSizeAsync(
            item.Episode.Uri,
            context.CancellationToken);

        string? seasonEpisodeLabel = BuildSeasonEpisodeLabel(item.Episode);
        progressNotifier.StartDownload(
            item.Episode.Program.Name,
            item.Episode.Title,
            seasonEpisodeLabel,
            totalSize: estimate?.EstimatedBytes);

        Progress<FfmpegProgressData> progress = new(data => progressNotifier.ReportProgress(data));

        try
        {
            await ffmpeg.DownloadAsync(item.Episode.Uri, filepath, item.Episode.Title, progress);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent download items from getting stuck in Downloading state
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "FFmpeg download failed for {Episode}", item.Episode.ToString());
            item.MarkFailed();
            progressNotifier.FailDownload();
            await dbContext.SaveChangesAsync();
            return;
        }

        item.MarkDownloaded();
        progressNotifier.CompleteDownload();

        await dbContext.SaveChangesAsync();

        if (item.Episode.TvdbEpisodes.Count == 0)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        try
        {
            IReadOnlyList<Series> sonarrSeries = await sonarr.GetSeriesAsync(context.CancellationToken);
            int? sonarrSeriesId = sonarrSeries
                .FirstOrDefault(s => s.TvdbId == item.Episode.Program.Series?.TvdbId)
                ?.Id;

            IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(
                settingsStore.Current.ResolvedEpisodeDownloadDirectory,
                sonarrSeriesId,
                context.CancellationToken);

            ManualImportFile file = manualImportFiles.First(x => x.Path.EndsWith(filename, StringComparison.OrdinalIgnoreCase));

            if (file.Series is null)
            {
                logger.LogWarning("Sonarr did not match {Episode} to a series. Skipping import", item.Episode.ToString());
                return;
            }

            if (file.Episodes.Count == 0)
            {
                logger.LogWarning("Sonarr matched {Episode} to series {SeriesId} but found no episodes. Skipping import", item.Episode.ToString(), file.Series.Id);
                return;
            }

            ManualImportRequest request = new(
                Path: file.Path,
                SeriesId: file.Series.Id,
                EpisodeIds: file.Episodes.Select(e => e.Id).ToList(),
                Quality: file.Quality,
                Languages: file.Languages,
                ReleaseGroup: "RUV");

            logger.LogInformation("Importing {Episode} into Sonarr", item.Episode.ToString());
            await sonarr.ManualImportFilesAsync([request]);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent download items from getting stuck in Downloading state
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Sonarr import failed for {Episode}", item.Episode.ToString());
            item.MarkFailed();
            await dbContext.SaveChangesAsync();
        }
    }

    private static string? BuildSeasonEpisodeLabel(RuvEpisode episode)
    {
        if (episode.TvdbEpisodes.Count == 0)
        {
            return null;
        }

        List<TvdbEpisode> ordered = [.. episode.TvdbEpisodes
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)];

        StringBuilder sb = new();
        sb.AppendFormat(CultureInfo.InvariantCulture, "S{0:D2}", ordered[0].SeasonNumber);
        foreach (TvdbEpisode ep in ordered)
        {
            sb.AppendFormat(CultureInfo.InvariantCulture, "E{0:D2}", ep.EpisodeNumber);
        }

        return sb.ToString();
    }
}