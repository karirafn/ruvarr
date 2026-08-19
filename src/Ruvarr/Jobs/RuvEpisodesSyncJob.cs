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
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class RuvEpisodesSyncJob(
    ILogger<RuvEpisodesSyncJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    ISonarrClient sonarr,
    ProgramRefreshNotifier syncQueue,
    IDomainEventBroadcaster broadcaster,
    ISettingsStore settingsStore) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        if (!settingsStore.Current.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping {JobName}: Sonarr is not configured", nameof(RuvEpisodesSyncJob));
            return;
        }

        logger.LogDebug("Starting RÚV episode sync job");

        List<int> ruvIds = [.. syncQueue.DequeueAll()];

        if (ruvIds is [])
        {
            logger.LogDebug("No programs in refresh queue");
            return;
        }

        CancellationToken cancellationToken = context.CancellationToken;

        var programs = await dbContext.Set<RuvProgram>()
            .AsNoTracking()
            .Where(x => ruvIds.Contains(x.RuvId))
            .Where(x => x.HasMultipleEpisodes)
            .Select(x => new { x.RuvId, x.Name })
            .ToListAsync(cancellationToken);

        HashSet<int> missingTvdbIds;
        IReadOnlyList<Series> sonarrSeries;

        try
        {
            missingTvdbIds = await sonarr.GetMissingTvdbIdsAsync(cancellationToken);
            sonarrSeries = await sonarr.GetSeriesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Catch all non-cancellation exceptions to prevent queue items from getting stuck in Processing state
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Sonarr calls failed during RÚV episode sync");

            foreach (int ruvId in ruvIds)
            {
                syncQueue.MarkComplete(ruvId);
            }

            broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
            return;
        }

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

        foreach (var projection in programs)
        {
            int ruvId = projection.RuvId;
            string programName = projection.Name;

            syncQueue.MarkProcessing(ruvId);

            try
            {
                await ProcessProgramAsync(
                    ruvId,
                    programName,
                    missingTvdbIds,
                    monitoredTvdbIds,
                    missingEpisodesTvdbIds,
                    cancellationToken);
            }
#pragma warning disable CA1031 // Catch all non-cancellation exceptions to prevent queue items from getting stuck in Processing state
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogError(
                    ex,
                    "Failed to process RÚV program '{ProgramName}' (RuvId: {RuvId}); skipping to next",
                    programName,
                    ruvId);

                dbContext.ChangeTracker.Clear();
                syncQueue.MarkComplete(ruvId);
                broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
                continue;
            }

            syncQueue.MarkComplete(ruvId);
            broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
        }

        foreach (int ruvId in ruvIds.Where(id => !loadedIds.Contains(id)))
        {
            syncQueue.MarkComplete(ruvId);
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }

    private async Task ProcessProgramAsync(
        int ruvId,
        string programName,
        HashSet<int> missingTvdbIds,
        HashSet<int> monitoredTvdbIds,
        HashSet<int> missingEpisodesTvdbIds,
        CancellationToken cancellationToken)
    {
        logger.LogDebug("Getting episodes for RÚV program '{Name}'", programName);

        if (await dbContext.FindRuvProgramAsync(ruvId, cancellationToken) is not RuvProgram program)
        {
            logger.LogDebug("RÚV program {RuvId} not found in database; skipping", ruvId);
            return;
        }

        RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(ruvId, cancellationToken);

        if (ruvProgram is null)
        {
            logger.LogInformation("Deleting RÚV program {Name} and {Count} episodes", programName, program.Episodes.Count);
            dbContext.Set<RuvProgram>().Remove(program);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        logger.LogDebug("Adding episodes to RÚV program '{Name}'", programName);

        foreach (RuvTvEpisode e in ruvProgram.Episodes)
        {
            bool added = program.TryAddEpisode(
                id: e.Id,
                uri: e.File,
                title: e.Title,
                description: e.Description.Count > 0 ? e.Description[0] : string.Empty,
                firstRun: e.FirstRun,
                duration: TimeSpan.FromSeconds(e.Duration));

            if (added)
            {
                RuvEpisode newEpisode = program.Episodes.First(ep => ep.RuvId == e.Id);
                logger.LogInformation("Added RÚV episode {Episode}", StripNewlines(newEpisode.ToString()));
            }
            else if (string.IsNullOrWhiteSpace(e.Title))
            {
                logger.LogInformation(
                    "Skipping titleless RÚV episode {EpisodeId} for program {ProgramName}",
                    StripNewlines(e.Id),
                    StripNewlines(programName));
            }
        }

        logger.LogDebug("Removing episodes from RÚV program '{Name}'", programName);
        List<RuvEpisode> removed = program.Episodes
            .Where(entity => !ruvProgram.Episodes.Select(episodeDto => episodeDto.Id).Contains(entity.RuvId))
            .ToList();

        foreach (RuvEpisode episode in removed)
        {
            logger.LogInformation("Removed RÚV episode {Episode}", StripNewlines(episode.ToString()));
            program.RemoveEpisode(episode);
        }

        foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbEpisodes.Count > 0))
        {
            episode.UpdateMissingStatus(missingTvdbIds);
        }

        bool isMonitored = program.Series is not null &&
            monitoredTvdbIds.Contains(program.Series.TvdbId);
        program.SetMonitoredStatus(isMonitored);

        bool hasMissingEpisodes = program.Series is not null &&
            missingEpisodesTvdbIds.Contains(program.Series.TvdbId);
        program.SetHasMissingEpisodes(hasMissingEpisodes);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string StripNewlines(string? value) =>
        value?.Replace("\r", string.Empty, StringComparison.Ordinal)
              .Replace("\n", string.Empty, StringComparison.Ordinal) ?? string.Empty;
}
