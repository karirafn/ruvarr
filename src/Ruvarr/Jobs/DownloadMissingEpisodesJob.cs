using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Downloads;
using Ruvarr.Ruv.Domain;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadMissingEpisodesJob(
    ILogger<DownloadMissingEpisodesJob> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting download missing episodes job");

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(pageSize: int.MaxValue)
            .ConfigureAwait(false);

        List<int?> missingEpisodeIds = [.. missingEpisodes.Select(x => x.TvdbId)];

        List<RuvEpisode> episodes = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Where(x => x.TvdbId != null)
            .Where(x => missingEpisodeIds.Contains(x.TvdbId))
            .Where(x => x.DownloadQueueItem == null)
            .OrderBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .ToListAsync()
            .ConfigureAwait(false);

        episodes.DistinctBy(x => x.RuvId)
            .ToList()
            .ForEach(dbContext.EnqueueDownload);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}