using System.Globalization;

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
internal sealed class DownloadUnmatchedMonitoredEpisodesJob(
    ILogger<DownloadUnmatchedMonitoredEpisodesJob> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IOptions<RuvarrOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting download unmatched monitored episodes job");

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync()
            .ConfigureAwait(false);

        List<int> monitoredSeriesTvdbIds = [.. missingEpisodes
            .Where(x => x.Monitored)
            .Select(x => x.SeriesId)
            .Distinct()];

        IReadOnlyList<Series> series = await sonarr.GetSeriesAsync()
            .ConfigureAwait(false);

        List<int> seriesIds = [.. series.Where(x => monitoredSeriesTvdbIds.Contains(x.Id))
            .Select(x => x.TvdbId)
            .Distinct()];

        List<RuvEpisode> ruvEpisodes = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Where(x => x.Program.Series != null)
            .Where(x => x.TvdbId == null)
            .Where(x => x.Downloaded == null)
            .Where(x => x.Title.StartsWith("Þáttur "))
            .ToListAsync()
            .ConfigureAwait(false);

        List<RuvEpisode> missingMonitoredRuvEpisodes = [.. ruvEpisodes.Where(x => seriesIds.Contains(int.Parse(x.Program.Series!.TvdbId, CultureInfo.InvariantCulture)))];

        foreach (RuvEpisode episode in missingMonitoredRuvEpisodes)
        {
            logger.LogInformation("Downloading {Program} {Title}", episode.Program.Series!.Name, episode.Title);
            string directory = Path.Join(options.Value.DownloadsRootDirectory, options.Value.EpisodeDownloadDirectory, episode.Program.Series!.Name);
            string filepath = Path.Join(directory, episode.ToFilename());

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await ffmpeg.DownloadAsync(episode.Uri, filepath, episode.Title)
                .ConfigureAwait(false);

            episode.MarkDownloaded();

            await dbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }

        if (missingMonitoredRuvEpisodes.Count > 0)
        {
            logger.LogInformation("Finished downloading {Count} episodes", missingMonitoredRuvEpisodes.Count);
        }
    }
}
