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
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        if (!settingsStore.Current.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping {JobName}: Sonarr is not configured", nameof(RuvEpisodesSyncJob));
            return;
        }

        logger.LogDebug("Starting RÚV episode sync job");

        List<IQueueLease> leases = [];
#pragma warning disable CA2000 // Loaded-item leases dispose via their using scope in the per-item foreach. Unloaded-id leases and the Sonarr-failure early-return path dispose via explicit loops.
        while (syncQueue.TryLeaseNext() is IQueueLease lease)
        {
            leases.Add(lease);
        }
#pragma warning restore CA2000

        if (leases is [])
        {
            logger.LogDebug("No programs in refresh queue");
            return;
        }

        List<int> ruvIds = [.. leases.Select(l => l.RuvId)];

        var programs = await dbContext.Set<RuvProgram>()
            .AsNoTracking()
            .Where(x => ruvIds.Contains(x.RuvId))
            .Where(x => x.HasMultipleEpisodes)
            .Select(x => new { x.RuvId, x.Name })
            .ToListAsync(cancellationToken);

        Result<IReadOnlyList<Series>> seriesResult;

        try
        {
            seriesResult = await sonarr.GetSeriesAsync(cancellationToken);
        }
#pragma warning disable CA1031 // Catch all non-cancellation exceptions to prevent queue items from getting stuck in Processing state
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Sonarr GetSeriesAsync threw during RÚV episode sync");
            ReleaseAll(leases);
            return;
        }

        if (seriesResult.IsFailure)
        {
            logger.LogError(
                "Sonarr GetSeriesAsync failed during RÚV episode sync ({ErrorCode}); skipping monitored/missing-episode and Sonarr episode status updates",
                seriesResult.Error.Code);
            ReleaseAll(leases);
            return;
        }

        IReadOnlyList<Series> sonarrSeries = seriesResult.FromResult();

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

        Dictionary<int, IQueueLease> leaseByRuvId = leases.ToDictionary(l => l.RuvId);

        foreach (var projection in programs)
        {
            int ruvId = projection.RuvId;
            string programName = projection.Name;

            syncQueue.MarkProcessing(ruvId);

            using IQueueLease programLease = leaseByRuvId[ruvId];

            try
            {
                await ProcessProgramAsync(
                    ruvId,
                    programName,
                    sonarrSeries,
                    monitoredTvdbIds,
                    missingEpisodesTvdbIds,
                    cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Defence-in-depth: GetProgramAsync no longer throws (ApiClient absorbs timeouts
                // into a Result), but this catch covers any future code path in the item body that
                // could throw a timeout. The lease guarantees completion regardless.
                logger.LogError(
                    ex,
                    "Transient timeout processing RÚV program '{ProgramName}' (RuvId: {RuvId}); skipping to next",
                    programName,
                    ruvId);

                dbContext.ChangeTracker.Clear();
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
            }

            broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
        }

        foreach (IQueueLease unloadedLease in leases.Where(l => !loadedIds.Contains(l.RuvId)))
        {
            unloadedLease.Dispose();
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }

    private void ReleaseAll(List<IQueueLease> leases)
    {
        foreach (IQueueLease lease in leases)
        {
            lease.Dispose();
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }

    private async Task ProcessProgramAsync(
        int ruvId,
        string programName,
        IReadOnlyList<Series> sonarrSeries,
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

        Result<RuvTvProgram> programResult = await ruv.GetProgramAsync(ruvId, cancellationToken);

        if (programResult.IsFailure && programResult.Error.Code == ApiClientErrors.NotFoundCode)
        {
            logger.LogInformation("Deleting RÚV program {Name} and {Count} episodes", programName, program.Episodes.Count);
            dbContext.Set<RuvProgram>().Remove(program);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (programResult.IsFailure)
        {
            logger.LogWarning(
                "Skipping RÚV program '{Name}' (RuvId: {RuvId}): API call failed ({ErrorCode})",
                programName,
                ruvId,
                programResult.Error.Code);
            return;
        }

        RuvTvProgram ruvProgram = programResult.FromResult();

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

        if (program.Series is not null)
        {
            await UpdateSonarrStatusAsync(program, program.Series.TvdbId, sonarrSeries, cancellationToken);
        }

        bool isMonitored = program.Series is not null &&
            monitoredTvdbIds.Contains(program.Series.TvdbId);
        program.SetMonitoredStatus(isMonitored);

        bool hasMissingEpisodes = program.Series is not null &&
            missingEpisodesTvdbIds.Contains(program.Series.TvdbId);
        program.SetHasMissingEpisodes(hasMissingEpisodes);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateSonarrStatusAsync(
        RuvProgram program,
        int tvdbSeriesId,
        IReadOnlyList<Series> sonarrSeries,
        CancellationToken cancellationToken)
    {
        List<RuvEpisode> matchedEpisodes = [.. program.Episodes.Where(x => x.TvdbEpisodes.Count > 0)];

        if (matchedEpisodes is [])
        {
            return;
        }

        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> statusResult =
            await sonarr.GetEpisodeStatusesAsync(tvdbSeriesId, sonarrSeries, cancellationToken);

        if (statusResult.IsFailure)
        {
            // Treating the failure as "not tracked" would clear every Missing status and
            // re-raise EpisodeMissingEvent for each one once Sonarr recovers.
            logger.LogWarning(
                "Sonarr episode status lookup failed for RÚV program '{Name}' ({ErrorCode}); keeping stored Sonarr statuses",
                program.Name,
                statusResult.Error.Code);
            return;
        }

        IReadOnlyDictionary<int, SonarrEpisodeStatus> statuses = statusResult.FromResult();

        foreach (RuvEpisode episode in matchedEpisodes)
        {
            episode.UpdateSonarrStatus(statuses);
        }
    }

    private static string StripNewlines(string? value) =>
        value?.Replace("\r", string.Empty, StringComparison.Ordinal)
              .Replace("\n", string.Empty, StringComparison.Ordinal) ?? string.Empty;
}