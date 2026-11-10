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
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
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

        try
        {
            RuvProgram? program = await dbContext.Set<RuvProgram>()
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                    .ThenInclude(x => x.TvdbEpisodes)
                .Include(x => x.Episodes)
                    .ThenInclude(x => x.DownloadQueueItems)
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

            Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> statusResult =
                await sonarr.GetEpisodeStatusesAsync(program.Series.TvdbId, cancellationToken);
            // Linking to TVDB does not depend on Sonarr; the next healthy RÚV episode sync sets the Sonarr status.
            SonarrEpisodeStatuses sonarrStatuses = statusResult.Match(
                SonarrEpisodeStatuses.From,
                error =>
                {
                    logger.LogWarning(
                        "Sonarr episode status lookup failed ({ErrorCode}); keeping stored Sonarr statuses until the next episode sync",
                        error.Code);
                    return SonarrEpisodeStatuses.Unavailable;
                });

            EpisodeMatchingContext matchingContext = new(program, seriesData, sonarrStatuses);

            if (program.MatchRecheckRequested)
            {
                await RecheckMatchesAsync(matchingContext, cancellationToken);
            }

            await matcher.MatchAsync(matchingContext, cancellationToken);
            program.CompleteMatchRecheck();

            await ScheduleLookupAsync(program, cancellationToken);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "HttpClient timeout during TVDB episode lookup for RuvId {RuvId}", ruvId);
        }
#pragma warning disable CA1031 // Catch all exceptions except OCE to prevent queue items from getting stuck in Processing state
        catch (Exception ex) when (ex is not OperationCanceledException)
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

    private async Task RecheckMatchesAsync(EpisodeMatchingContext context, CancellationToken cancellationToken)
    {
        // Manual links are never judged: a correction the rule disagrees with would otherwise be undone on every refresh.
        List<RuvEpisode> automaticEpisodes = [.. context.Program.Episodes
            .Where(x => x.MatchOrigin == MatchOrigin.Automatic && x.TvdbEpisodes.Count == 1)];

        if (automaticEpisodes.Count == 0)
        {
            return;
        }

        IReadOnlyList<IslTitle> titles = await context.IslTitles.GetAsync(tvdb, context.SeriesData.Episodes, cancellationToken);
        SeasonConstraint constraint = context.SeasonConstraint;

        foreach (RuvEpisode episode in automaticEpisodes)
        {
            MatchVerdict verdict = constraint.Judge(episode.TvdbEpisodes[0], IslTitleLookup.CandidatesFor(episode, titles));

            if (verdict != MatchVerdict.Rejected)
            {
                continue;
            }

            string? previousLabel = episode.SeasonEpisodeLabel;
            episode.Unmatch();

            // Ruvarr does not touch Sonarr, so a completed download may stay imported under the wrong episode.
            logger.LogWarning(
                "Match re-check unmatched RÚV episode {Episode} from {PreviousSeasonEpisode}; download status: {DownloadStatus}",
                episode.ToString(),
                previousLabel,
                DownloadStatusOf(episode));
        }
    }

    private static string DownloadStatusOf(RuvEpisode episode) =>
        episode.DownloadQueueItems
            .OrderByDescending(x => x.Created)
            .Select(x => x.Status.ToString())
            .FirstOrDefault() ?? "None";

    private async Task ScheduleLookupAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbEpisodes.Count == 0))
        {
            episode.ScheduleLookup();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}