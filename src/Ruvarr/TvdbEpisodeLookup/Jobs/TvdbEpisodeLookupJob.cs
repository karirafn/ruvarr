using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.TvdbEpisodeLookup.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbEpisodeLookupJob(
    ILogger<TvdbEpisodeLookupJob> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    ISonarrClient sonarr,
    TvdbEpisodeLookupNotifier lookupQueue,
    IDomainEventBroadcaster broadcaster,
    ISettingsStore settingsStore,
    ITvdbEpisodeMatcher matcher) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!settingsStore.Current.IsTvdbConfigured || !settingsStore.Current.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping {JobName}: TVDB or Sonarr is not configured", nameof(TvdbEpisodeLookupJob));
            return;
        }

        logger.LogDebug("Starting TVDB episode lookup job");

        if (!lookupQueue.TryDequeue(out int ruvId))
        {
            logger.LogDebug("No RÚV program pending TVDB episode lookup");
            return;
        }

        lookupQueue.MarkProcessing(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());

        CancellationToken cancellationToken = context.CancellationToken;

        try
        {
            RuvProgram? program = await dbContext.Set<RuvProgram>()
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                    .ThenInclude(x => x.TvdbEpisodes)
                .Where(x => x.RuvId == ruvId)
                .FirstOrDefaultAsync(cancellationToken);

            if (program is null || program.Series is null)
            {
                logger.LogDebug("No RÚV program pending TVDB episode lookup");
                return;
            }

            logger.LogDebug("Getting TVDB series data");
            SeriesData? seriesData = await tvdb.GetSeriesAsync(program.Series.TvdbId, cancellationToken: cancellationToken);

            if (seriesData is null)
            {
                await ScheduleLookupAsync(program, cancellationToken);
                return;
            }

            HashSet<int> missingTvdbIds = await sonarr.GetMissingTvdbIdsAsync(cancellationToken);

            EpisodeMatchingContext matchingContext = new(program, seriesData, missingTvdbIds);
            await matcher.MatchAsync(matchingContext, cancellationToken);

            await ScheduleLookupAsync(program, cancellationToken);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent queue items from getting stuck in Processing state
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Error during TVDB episode lookup for RuvId {RuvId}", ruvId);
        }
        finally
        {
            lookupQueue.MarkComplete(ruvId);
            broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
        }
    }

    private async Task ScheduleLookupAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbEpisodes.Count == 0))
        {
            episode.ScheduleLookup();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
