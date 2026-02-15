
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.FFmpeg;
using Ruvarr.Ruv.Domain;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Sonarr.Jobs;

[DisallowConcurrentExecution]
internal class DownloadMissingEpisodesJob(
    ILogger<DownloadMissingEpisodesJob> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    IFfmpegService ffmpeg,
    IOptions<RuvarrOptions> options) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync()
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

            string filename = $"{episode.Program.Series!.Name} S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}.mp4";
            string filepath = Path.Join(options.Value.EpisodeDownloadDirectory, filename);
            await ffmpeg.DownloadAsync(episode.Uri, filepath, episode.Title)
                .ConfigureAwait(false);

            episode.MarkDownloaded();

            await dbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }
    }
}