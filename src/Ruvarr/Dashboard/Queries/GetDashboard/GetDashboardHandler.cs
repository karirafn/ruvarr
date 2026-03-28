using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Dashboard.Queries.GetDashboard;

internal sealed class GetDashboardHandler(
    IServiceScopeFactory serviceScopeFactory,
    TvdbSeriesLookupNotifier tvdbSeriesLookupNotifier,
    TvdbEpisodeLookupNotifier tvdbEpisodeLookupNotifier,
    ProgramRefreshNotifier programRefreshNotifier)
    : IRequestHandler<GetDashboardQuery, DashboardData>
{
    private const int EpisodeTableLimit = 10;
    private const int LikelyDownloadedCandidateLimit = 50;
    private const int SevenDays = 7;

    public async Task<DashboardData> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        Task<IReadOnlyList<DashboardEpisodeItem>> recentlyAddedTask = GetRecentlyAddedEpisodesAsync(cancellationToken);
        Task<IReadOnlyList<DashboardEpisodeItem>> requiresTranslationTask = GetRequiresTranslationEpisodesAsync(cancellationToken);
        Task<IReadOnlyList<DashboardEpisodeItem>> likelyDownloadedTask = GetLikelyDownloadedOnceMatchedAsync(cancellationToken);
        Task<DashboardStatistics> statisticsTask = GetStatisticsAsync(cancellationToken);

        await Task.WhenAll(recentlyAddedTask, requiresTranslationTask, likelyDownloadedTask, statisticsTask);

        DashboardQueueStatus queueStatus = await GetQueueStatusAsync(cancellationToken);

        return new DashboardData(
            await recentlyAddedTask,
            await requiresTranslationTask,
            await likelyDownloadedTask,
            await statisticsTask,
            queueStatus);
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
        Task<DownloadStatistics> downloadsTask = GetDownloadStatisticsAsync(cancellationToken);

        await Task.WhenAll(programsTask, episodesTask, downloadsTask);

        return new DashboardStatistics(await programsTask, await episodesTask, await downloadsTask);
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

    private async Task<DownloadStatistics> GetDownloadStatisticsAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        int queueDepth = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Pending || x.Status == DownloadQueueStatus.Downloading)
            .CountAsync(cancellationToken);

        int downloading = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Downloading)
            .CountAsync(cancellationToken);

        DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-SevenDays);
        int completedLast7Days = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Complete && x.Downloaded >= sevenDaysAgo)
            .CountAsync(cancellationToken);

        int failed = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Failed)
            .CountAsync(cancellationToken);

        return new DownloadStatistics(queueDepth, downloading, completedLast7Days, failed);
    }

    private async Task<DashboardQueueStatus> GetQueueStatusAsync(CancellationToken cancellationToken)
    {
        return new DashboardQueueStatus(
            ToQueueInfo(tvdbSeriesLookupNotifier.Items),
            ToQueueInfo(tvdbEpisodeLookupNotifier.Items),
            ToQueueInfo(programRefreshNotifier.Items),
            await GetDownloadQueueInfoAsync(cancellationToken));
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
