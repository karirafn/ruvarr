using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Dashboard.Queries.GetDashboard;

internal sealed class GetDashboardHandler(
    IServiceScopeFactory serviceScopeFactory,
    TvdbSeriesLookupNotifier tvdbSeriesLookupNotifier,
    TvdbEpisodeLookupNotifier tvdbEpisodeLookupNotifier,
    ProgramRefreshNotifier programRefreshNotifier,
    DownloadProgressNotifier downloadProgressNotifier,
    ISchedulerFactory schedulerFactory)
    : IRequestHandler<GetDashboardQuery, DashboardData>
{
    private const int EpisodeTableLimit = 10;
    private const int LikelyDownloadedCandidateLimit = 50;

    public async Task<DashboardData> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<DashboardEpisodeItem>> recentlyAddedTask = GetRecentlyAddedEpisodesAsync(cancellationToken);
        Task<IReadOnlyList<DashboardEpisodeItem>> requiresTranslationTask = GetRequiresTranslationEpisodesAsync(cancellationToken);
        Task<IReadOnlyList<DashboardEpisodeItem>> likelyDownloadedTask = GetLikelyDownloadedOnceMatchedAsync(cancellationToken);
        Task<DashboardStatistics> statisticsTask = GetStatisticsAsync(cancellationToken);

        await Task.WhenAll(recentlyAddedTask, requiresTranslationTask, likelyDownloadedTask, statisticsTask);

        Task<DashboardQueueStatus> queueStatusTask = GetQueueStatusAsync(cancellationToken);
        Task<ProgramRefreshCardInfo> programRefreshTask = GetProgramRefreshCardInfoAsync(cancellationToken);
        Task<TvdbSeriesLookupCardInfo> tvdbSeriesLookupTask = GetTvdbSeriesLookupCardInfoAsync(cancellationToken);
        Task<TvdbEpisodeLookupCardInfo> tvdbEpisodeLookupTask = GetTvdbEpisodeLookupCardInfoAsync(cancellationToken);
        Task<DownloadCardInfo> downloadCardTask = GetDownloadCardInfoAsync(cancellationToken);

        await Task.WhenAll(queueStatusTask, programRefreshTask, tvdbSeriesLookupTask, tvdbEpisodeLookupTask, downloadCardTask);

        return new DashboardData(
            await recentlyAddedTask,
            await requiresTranslationTask,
            await likelyDownloadedTask,
            await statisticsTask,
            await queueStatusTask,
            await programRefreshTask,
            await tvdbSeriesLookupTask,
            await tvdbEpisodeLookupTask,
            await downloadCardTask);
    }

    private async Task<IReadOnlyList<DashboardEpisodeItem>> GetRecentlyAddedEpisodesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        return await dbContext.Set<RuvEpisode>()
            .Where(e => e.Program.Series != null || e.Program.Movie != null)
            .OrderByDescending(e => e.FirstRun)
            .Take(EpisodeTableLimit)
            .Select(e => new DashboardEpisodeItem(
                e.Program.Name,
                e.Program.RuvId,
                e.Title,
                e.FirstRun))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardEpisodeItem>> GetRequiresTranslationEpisodesAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        return await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count > 0)
            .Where(e => !e.TvdbEpisodes.Any(te => te.HasIslTranslation))
            .OrderByDescending(e => e.FirstRun)
            .Take(EpisodeTableLimit)
            .Select(e => new DashboardEpisodeItem(
                e.Program.Name,
                e.Program.RuvId,
                e.Title,
                e.FirstRun))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<DashboardEpisodeItem>> GetLikelyDownloadedOnceMatchedAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        List<DashboardEpisodeItem> candidates = await dbContext.Set<RuvEpisode>()
            .Where(e => e.Program.IsMonitored)
            .Where(e => e.Program.HasMissingEpisodes)
            .Where(e => e.Program.Series != null)
            .Where(e => e.TvdbEpisodes.Count == 0)
            .OrderByDescending(e => e.FirstRun)
            .Take(LikelyDownloadedCandidateLimit)
            .Select(e => new DashboardEpisodeItem(
                e.Program.Name,
                e.Program.RuvId,
                e.Title,
                e.FirstRun))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(e => !RuvProgram.IsGenericEpisodeTitle(e.EpisodeTitle))
            .Take(EpisodeTableLimit)
            .ToList();
    }

    private async Task<DashboardStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
    {
        Task<ProgramStatistics> programsTask = GetProgramStatisticsAsync(cancellationToken);
        Task<EpisodeStatistics> episodesTask = GetEpisodeStatisticsAsync(cancellationToken);

        await Task.WhenAll(programsTask, episodesTask);

        return new DashboardStatistics(await programsTask, await episodesTask);
    }

    private async Task<ProgramStatistics> GetProgramStatisticsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int total = await dbContext.Set<RuvProgram>()
            .CountAsync(cancellationToken);

        int monitored = await dbContext.Set<RuvProgram>()
            .Where(p => p.IsMonitored)
            .CountAsync(cancellationToken);

        int matched = await dbContext.Set<RuvProgram>()
            .Where(p => p.Series != null || p.Movie != null)
            .CountAsync(cancellationToken);

        int withMissingEpisodes = await dbContext.Set<RuvProgram>()
            .Where(p => p.HasMissingEpisodes)
            .CountAsync(cancellationToken);

        return new ProgramStatistics(total, monitored, matched, withMissingEpisodes);
    }

    private async Task<EpisodeStatistics> GetEpisodeStatisticsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int total = await dbContext.Set<RuvEpisode>()
            .CountAsync(cancellationToken);

        int matched = await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count > 0)
            .CountAsync(cancellationToken);

        int unmatched = await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count == 0)
            .CountAsync(cancellationToken);

        int withoutTranslation = await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count > 0)
            .Where(e => !e.TvdbEpisodes.Any(te => te.HasIslTranslation))
            .CountAsync(cancellationToken);

        return new EpisodeStatistics(total, matched, unmatched, withoutTranslation);
    }

    private async Task<DownloadCardInfo> GetDownloadCardInfoAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int pendingCount = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Pending)
            .CountAsync(cancellationToken);

        int queueDepth = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Pending || x.Status == DownloadQueueStatus.Downloading)
            .CountAsync(cancellationToken);

        List<DownloadCardPendingItem> pendingItems = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Pending)
            .OrderBy(x => x.Created)
            .Select(x => new DownloadCardPendingItem(x.Episode.Program.Name, x.Episode.Title))
            .ToListAsync(cancellationToken);

        return new DownloadCardInfo(
            downloadProgressNotifier.IsDownloading,
            downloadProgressNotifier.ProgramName,
            downloadProgressNotifier.EpisodeTitle,
            downloadProgressNotifier.SeasonEpisodeLabel,
            pendingCount,
            downloadProgressNotifier.BytesDownloaded,
            downloadProgressNotifier.TotalSize,
            downloadProgressNotifier.RateBytesPerSecond,
            downloadProgressNotifier.EstimatedRemaining,
            pendingItems,
            downloadProgressNotifier.LastDownloadedAt,
            downloadProgressNotifier.CompletedLast7Days,
            downloadProgressNotifier.FailedCount,
            queueDepth);
    }

    private async Task<DashboardQueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken)
    {
        return new DashboardQueueStatus(
            ToQueueInfo(tvdbSeriesLookupNotifier.Items),
            ToQueueInfo(tvdbEpisodeLookupNotifier.Items),
            await GetDownloadQueueInfoAsync(cancellationToken));
    }

    private async Task<ProgramRefreshCardInfo> GetProgramRefreshCardInfoAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = programRefreshNotifier.Items;
        bool isRunning = items.Count > 0;
        int depth = items.Count;

        DateTimeOffset? nextFireTimeUtc = null;
        try
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(
                new JobKey(nameof(RuvProgramRefreshJob)), cancellationToken);
            nextFireTimeUtc = triggers.FirstOrDefault()?.GetNextFireTimeUtc();
        }
        catch (SchedulerException)
        {
            // Scheduler not started or job not found
        }

        return new ProgramRefreshCardInfo(
            isRunning,
            depth,
            programRefreshNotifier.CompletedCount,
            programRefreshNotifier.CurrentProgram,
            programRefreshNotifier.LastCompletedAt,
            programRefreshNotifier.LastRunDuration,
            programRefreshNotifier.LastRunTotal,
            nextFireTimeUtc);
    }

    private async Task<TvdbSeriesLookupCardInfo> GetTvdbSeriesLookupCardInfoAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = tvdbSeriesLookupNotifier.Items;
        bool isProcessing = items.Any(x => x.IsProcessing);
        int pendingCount = items.Count(x => !x.IsProcessing);

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int retryCount = await dbContext.Set<RuvProgram>()
            .CountAsync(x => x.NextLookup != null, cancellationToken);

        return new TvdbSeriesLookupCardInfo(
            isProcessing,
            tvdbSeriesLookupNotifier.CurrentProgram,
            pendingCount,
            tvdbSeriesLookupNotifier.LastLookedUpAt,
            retryCount);
    }

    private async Task<TvdbEpisodeLookupCardInfo> GetTvdbEpisodeLookupCardInfoAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = tvdbEpisodeLookupNotifier.Items;
        bool isProcessing = items.Any(x => x.IsProcessing);
        int pendingCount = items.Count(x => !x.IsProcessing);

        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int retryCount = await dbContext.Set<RuvEpisode>()
            .CountAsync(x => x.NextLookup != null, cancellationToken);

        return new TvdbEpisodeLookupCardInfo(
            isProcessing,
            tvdbEpisodeLookupNotifier.CurrentProgram,
            pendingCount,
            tvdbEpisodeLookupNotifier.LastLookedUpAt,
            retryCount);
    }

    private static DashboardQueueInfo ToQueueInfo<T>(IReadOnlyList<T> items) where T : IQueueItemSummary
    {
        T? activeItem = items.FirstOrDefault(x => x.IsProcessing);
        return new DashboardQueueInfo(items.Count, activeItem?.ProgramName);
    }

    private async Task<DashboardQueueInfo> GetDownloadQueueInfoAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int depth = await dbContext.Set<DownloadQueueItem>()
            .CountAsync(x => x.Status == DownloadQueueStatus.Pending || x.Status == DownloadQueueStatus.Downloading, cancellationToken);

        string? activeItem = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Downloading)
            .Select(x => x.Episode.Program.Name + " - " + x.Episode.Title)
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardQueueInfo(depth, activeItem);
    }
}
