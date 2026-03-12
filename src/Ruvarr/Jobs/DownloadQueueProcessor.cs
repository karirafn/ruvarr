
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.FFmpeg;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal class DownloadQueueProcessor(
    ILogger<DownloadQueueProcessor> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IOptions<RuvarrOptions> options,
    DownloadQueueNotifier notifier) : IJob
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

        item.MarkDownloading();
        await dbContext.SaveChangesAsync();
        notifier.Notify();

        logger.LogInformation("Downloading {Program} - {Title}", item.Episode.Program.Name, item.Episode.Title);
        string filename = item.Episode.ToFilename();
        string directory = Path.Join(
            options.Value.DownloadsRootDirectory,
            options.Value.EpisodeDownloadDirectory,
            item.Episode.Program.Series?.Name ?? item.Episode.Program.Name);
        string filepath = Path.Join(directory, filename);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(filepath))
        {
            string extension = Path.GetExtension(filename);
            string filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            filename = $"{filenameWithoutExtension}.X{extension}";
            filepath = Path.Join(directory, filename);
        }

        await ffmpeg.DownloadAsync(item.Episode.Uri, filepath, item.Episode.Title);

        item.MarkDownloaded();

        await dbContext.SaveChangesAsync();
        notifier.Notify();

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

        IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(options.Value.EpisodeDownloadDirectory);

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