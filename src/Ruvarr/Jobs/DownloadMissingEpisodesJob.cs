using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.FFmpeg;
using Ruvarr.Ruv.Domain;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadMissingEpisodesJob(
    ILogger<DownloadMissingEpisodesJob> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IOptions<RuvarrOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(pageSize: int.MaxValue)
            .ConfigureAwait(false);

        List<int?> missingEpisodeIds = [.. missingEpisodes.Select(x => x.TvdbId)];

        List<RuvEpisode> episodes = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Where(x => x.TvdbId != null)
            .Where(x => missingEpisodeIds.Contains(x.TvdbId))
            .Where(x => x.Downloaded == null)
            .OrderBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (RuvEpisode episode in episodes)
        {
            logger.LogInformation(
                "Downloading {Program} S{Season:D2}E{Episode:D2} {Title}",
                episode.Program.Name,
                episode.SeasonNumber,
                episode.EpisodeNumber,
                episode.Title);

            MissingEpisode missingEpisode = missingEpisodes.First(x => x.TvdbId == episode.TvdbId);

            string filepath = Path.Join(options.Value.DownloadsRootDirectory, options.Value.EpisodeDownloadDirectory, episode.ToFilename());
            await ffmpeg.DownloadAsync(episode.Uri, filepath, episode.Title)
                .ConfigureAwait(false);

            IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(options.Value.EpisodeDownloadDirectory)
                .ConfigureAwait(false);

            string importPath = Path.Join(options.Value.EpisodeDownloadDirectory, episode.ToFilename());
            ManualImportFile file = manualImportFiles.First(x => x.Path == importPath);

            ManualImportRequest request = new(
                Path: importPath,
                SeriesId: missingEpisode.SeriesId,
                EpisodeIds: [missingEpisode.Id],
                Quality: file.Quality,
                Languages: file.Languages,
                ReleaseGroup: "RÚV");
            await sonarr.ManualImportFilesAsync([request])
                .ConfigureAwait(false);

            episode.MarkDownloaded();

            await dbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }

        if (episodes.Count > 0)
        {
            logger.LogInformation("Finished downloading {Count} episodes", episodes.Count);
        }
    }
}