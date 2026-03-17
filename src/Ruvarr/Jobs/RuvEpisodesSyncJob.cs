using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class RuvEpisodesSyncJob(
    ILogger<RuvEpisodesSyncJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    ProgramRefreshNotifier syncQueue,
    IDomainEventBroadcaster broadcaster) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV episode sync job");

        List<int> ruvIds = [.. syncQueue.DequeueAll()];

        if (ruvIds is [])
        {
            logger.LogDebug("No programs in refresh queue");
            return;
        }

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => ruvIds.Contains(x.RuvId))
            .Where(x => x.HasMultipleEpisodes)
            .ToListAsync();

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync();
        HashSet<int> missingTvdbIds = [.. missingEpisodes.Select(x => x.TvdbId)];

        IReadOnlyList<Series> sonarrSeries = await sonarr.GetSeriesAsync();
        HashSet<int> monitoredTvdbIds = [.. sonarrSeries
            .Where(x => x.Monitored)
            .Select(x => x.TvdbId)];
        HashSet<int> missingEpisodesTvdbIds = [.. sonarrSeries
            .Where(x => x.Seasons.Where(s => s.SeasonNumber > 0).Any(s => s.Statistics.PercentOfEpisodes < 1))
            .Select(x => x.TvdbId)];
#pragma warning disable CA1309 // Culture-sensitive comparison is intentional for Icelandic alphabetical ordering
        programs.Sort((a, b) => string.Compare(a.Name, b.Name, new CultureInfo("is-IS"), CompareOptions.None));
#pragma warning restore CA1309
        logger.LogDebug("Found {Count} RÚV programs with multiple episodes in refresh queue", programs.Count);

        HashSet<int> loadedIds = [.. programs.Select(p => p.RuvId)];

        foreach (RuvProgram program in programs)
        {
            syncQueue.MarkProcessing(program.RuvId);

            logger.LogDebug("Getting episodes for RÚV program '{Name}'", program.Name);

            RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(program.RuvId);

            if (ruvProgram is null)
            {
                logger.LogInformation("Deleting RÚV program {Name} and {Count} episodes", program.Name, program.Episodes.Count);
                dbContext.Set<RuvProgram>().Remove(program);
                await dbContext.SaveChangesAsync();
                syncQueue.MarkComplete(program.RuvId);
                continue;
            }

            logger.LogDebug("Adding episodes to RÚV program '{Name}'", program.Name);

            ruvProgram.Episodes
                .Where(e => program.TryAddEpisode(
                    id: e.Id,
                    uri: e.File,
                    title: e.Title,
                    description: e.Description.Count > 0 ? e.Description[0] : string.Empty,
                    firstRun: e.FirstRun,
                    durationSeconds: e.Duration))
                .ToList()
                .ForEach(e => logger.LogInformation("Added RÚV episode '{EpisodeName}' to program '{Name}'", e.Title, program.Name));

            logger.LogDebug("Removing episodes from RÚV program '{Name}'", program.Name);
            IEnumerable<RuvEpisode> removed = program.Episodes
                .Where(entity => !ruvProgram.Episodes.Select(episodeDto => episodeDto.Id).Contains(entity.RuvId));

            foreach (RuvEpisode episode in removed)
            {
                logger.LogInformation("Removed RÚV episode '{EpisodeName}' from program '{Name}'", episode.Title, program.Name);
                program.RemoveEpisode(episode);
            }

            foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbId is not null))
            {
                episode.SetMissing(missingTvdbIds.Contains(episode.TvdbId!.Value));
            }

            bool isMonitored = program.Series is not null &&
                monitoredTvdbIds.Contains(program.Series.TvdbId);
            program.SetMonitoredStatus(isMonitored);

            bool hasMissingEpisodes = program.Series is not null &&
                missingEpisodesTvdbIds.Contains(program.Series.TvdbId);
            program.SetHasMissingEpisodes(hasMissingEpisodes);

            await dbContext.SaveChangesAsync();
            syncQueue.MarkComplete(program.RuvId);
        }

        foreach (int ruvId in ruvIds.Where(id => !loadedIds.Contains(id)))
        {
            syncQueue.MarkComplete(ruvId);
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }
}