
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
            .Where(x => x.Status == DownloadQueueStatus.Pending)
            .OrderBy(x => x.Created)
            .FirstOrDefaultAsync();

        if (item is null)
        {
            logger.LogDebug("No pending items in download queue");
            return;
        }

        if (item.Episode is null)
        {
            logger.LogWarning("Download queue item has no associated episode, removing orphaned item");
            await dbContext.Set<DownloadQueueItem>()
                .Where(x => x.Episode == null)
                .ExecuteDeleteAsync();
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

        logger.LogInformation("Downloading {Program} - {Title}", item.Episode.Program.Name, item.Episode.Title);
        string tentativePath = item.Episode.ToFilePath(
            downloadsRootDirectory,
            episodeDownloadDirectory,
            fileAlreadyExists: false);

        string filepath = item.Episode.ToFilePath(
            downloadsRootDirectory,
            episodeDownloadDirectory,
            fileAlreadyExists: File.Exists(tentativePath));

        string? directory = Path.GetDirectoryName(filepath);
        if (directory is not null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string filename = Path.GetFileName(filepath);

        await ffmpeg.DownloadAsync(item.Episode.Uri, filepath, item.Episode.Title);

        item.MarkDownloaded();

        await dbContext.SaveChangesAsync();

        if (item.Episode.TvdbId == null)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync();

        MissingEpisode? missingEpisode = missingEpisodes.FirstOrDefault(x => x.TvdbId == item.Episode.TvdbId);

        if (missingEpisode is null)
        {
            logger.LogError(
                "{Program} S{Season:D2}E{Episode:D2} {Title} was scheduled for manual import into Sonarr but was not found in missing episodes.",
                item.Episode.Program.Name,
                item.Episode.SeasonNumber,
                item.Episode.EpisodeNumber,
                item.Episode.Title);
            return;
        }

        IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(episodeDownloadDirectory);

        ManualImportFile file = manualImportFiles.First(x => x.Path.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        ManualImportRequest request = new(
            Path: file.Path,
            SeriesId: missingEpisode.SeriesId,
            EpisodeIds: [missingEpisode.Id],
            Quality: file.Quality,
            Languages: file.Languages,
            ReleaseGroup: "RUV");

        logger.LogInformation("Importing {Program} S{Season:D2}E{Episode:D2} {Title} into Sonarr",
            item.Episode.Program.Name,
            item.Episode.SeasonNumber,
            item.Episode.EpisodeNumber,
            item.Episode.Title);
        await sonarr.ManualImportFilesAsync([request]);
    }
}