
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

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
    IOptions<RuvarrOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting download queue processor job");

        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Include(x => x.Episode)
            .ThenInclude(x => x.Program)
            .Where(x => x.Downloaded == null)
            .OrderBy(x => x.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (item is null)
        {
            logger.LogDebug("No pending items in download queue");
            return;
        }

        logger.LogInformation("Downloading {Program} {Title}", item.Episode.Program.Name, item.Episode.Title);
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

        await ffmpeg.DownloadAsync(item.Episode.Uri, filepath, item.Episode.Title)
            .ConfigureAwait(false);

        item.MarkDownloaded();

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);

        if (item.Episode.TvdbId == null)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync()
            .ConfigureAwait(false);

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

        IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(options.Value.EpisodeDownloadDirectory)
            .ConfigureAwait(false);

        string importPath = Path.Join(options.Value.EpisodeDownloadDirectory, item.Episode.Program.Name, filename);
        ManualImportFile file = manualImportFiles.First(x => x.Path.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
        ManualImportRequest request = new(
            Path: importPath,
            SeriesId: missingEpisode.SeriesId,
            EpisodeIds: [missingEpisode.Id],
            Quality: file.Quality,
            Languages: file.Languages,
            ReleaseGroup: "RUV");

        logger.LogDebug("Starting manual import of {Program} S{Season:D2}E{Episode:D2} {Title} into Sonarr",
            item.Episode.Program.Name,
            item.Episode.SeasonNumber,
            item.Episode.EpisodeNumber,
            item.Episode.Title);
        await sonarr.ManualImportFilesAsync([request])
            .ConfigureAwait(false);
    }
}