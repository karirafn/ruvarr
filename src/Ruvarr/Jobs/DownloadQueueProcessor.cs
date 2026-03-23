
using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal class DownloadQueueProcessor(
    ILogger<DownloadQueueProcessor> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    ISettingsStore settingsStore) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
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

        string downloadsRootDirectory = settingsStore.Current.DownloadsRootDirectory;

        if (string.IsNullOrEmpty(downloadsRootDirectory))
        {
            logger.LogWarning("Downloads root directory is not configured. Skipping download");
            return;
        }

        string episodeDownloadDirectory = settingsStore.Current.EpisodeDownloadDirectory;

        if (string.IsNullOrEmpty(episodeDownloadDirectory))
        {
            logger.LogWarning("Episode download directory is not configured. Skipping download");
            return;
        }

        logger.LogInformation("Downloading {Episode}", item.Episode.ToString());

        string filepath;
        try
        {
            string tentativePath = item.Episode.ToFilePath(
                downloadsRootDirectory,
                episodeDownloadDirectory,
                fileAlreadyExists: false);

            filepath = item.Episode.ToFilePath(
                downloadsRootDirectory,
                episodeDownloadDirectory,
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

        await ffmpeg.DownloadAsync(item.Episode.Uri, filepath, item.Episode.Title);

        item.MarkDownloaded();

        await dbContext.SaveChangesAsync();

        if (item.Episode.TvdbEpisodes.Count == 0)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync();
        Dictionary<int, MissingEpisode> missingByTvdbId = missingEpisodes.ToDictionary(x => x.TvdbId);

        List<int> episodeIds = [.. item.Episode.TvdbEpisodes
            .Select(e => missingByTvdbId.GetValueOrDefault(e.TvdbId))
            .OfType<MissingEpisode>()
            .Select(m => m.Id)];

        int? seriesId = item.Episode.TvdbEpisodes
            .Select(e => missingByTvdbId.GetValueOrDefault(e.TvdbId))
            .OfType<MissingEpisode>()
            .Select(m => (int?)m.SeriesId)
            .FirstOrDefault();

        if (episodeIds.Count == 0 || seriesId is null)
        {
            logger.LogError(
                "{Episode} was scheduled for manual import into Sonarr but none of its matched episodes were found in missing episodes.",
                item.Episode.ToString());
            return;
        }

        IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(episodeDownloadDirectory);

        ManualImportFile file = manualImportFiles.First(x => x.Path.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        ManualImportRequest request = new(
            Path: file.Path,
            SeriesId: seriesId.Value,
            EpisodeIds: episodeIds,
            Quality: file.Quality,
            Languages: file.Languages,
            ReleaseGroup: "RUV");

        logger.LogInformation("Importing {Episode} into Sonarr", item.Episode.ToString());
        await sonarr.ManualImportFilesAsync([request]);
    }
}