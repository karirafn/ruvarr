using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Downloads;
using Ruvarr.Ruv.Domain;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class DownloadUnmatchedMonitoredEpisodesJob(
    ILogger<DownloadUnmatchedMonitoredEpisodesJob> logger,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr) : IJob
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
            .Where(x => x.DownloadQueueItem == null)
            .Where(x => x.Title.StartsWith("Þáttur "))
            .ToListAsync()
            .ConfigureAwait(false);

        ruvEpisodes
            .Where(x => seriesIds.Contains(int.Parse(x.Program.Series!.TvdbId, CultureInfo.InvariantCulture)))
            .DistinctBy(x => x.RuvId)
            .ToList()
            .ForEach(dbContext.EnqueueDownload);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}
