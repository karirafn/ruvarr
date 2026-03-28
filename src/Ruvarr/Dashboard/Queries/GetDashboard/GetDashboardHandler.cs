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
    RuvarrDbContext dbContext,
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
        IReadOnlyList<DashboardEpisodeItem> recentlyAdded = await GetRecentlyAddedEpisodesAsync(cancellationToken);
        IReadOnlyList<DashboardEpisodeItem> requiresTranslation = await GetRequiresTranslationEpisodesAsync(cancellationToken);
        IReadOnlyList<DashboardEpisodeItem> likelyDownloaded = await GetLikelyDownloadedOnceMatchedAsync(cancellationToken);
        DashboardStatistics statistics = await GetStatisticsAsync(cancellationToken);
        DashboardQueueStatus queueStatus = GetQueueStatus();

        return new DashboardData(recentlyAdded, requiresTranslation, likelyDownloaded, statistics, queueStatus);
    }

    private async Task<IReadOnlyList<DashboardEpisodeItem>> GetRecentlyAddedEpisodesAsync(CancellationToken cancellationToken)
    {
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
        int totalPrograms = await dbContext.Set<RuvProgram>()
            .CountAsync(cancellationToken);

        int totalEpisodes = await dbContext.Set<RuvEpisode>()
            .CountAsync(cancellationToken);

        int unmatchedEpisodeCount = await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count == 0)
            .CountAsync(cancellationToken);

        int missingTranslationCount = await dbContext.Set<RuvEpisode>()
            .Where(e => e.TvdbEpisodes.Count > 0)
            .Where(e => !e.TvdbEpisodes.Any(te => te.HasIslTranslation))
            .CountAsync(cancellationToken);

        int activeDownloadQueueDepth = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status != DownloadQueueStatus.Complete)
            .CountAsync(cancellationToken);

        int programsWithMissingEpisodes = await dbContext.Set<RuvProgram>()
            .Where(p => p.HasMissingEpisodes)
            .CountAsync(cancellationToken);

        DateTime sevenDaysAgo = DateTime.UtcNow.AddDays(-SevenDays);
        int downloadsCompletedLast7Days = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Complete && x.Downloaded >= sevenDaysAgo)
            .CountAsync(cancellationToken);

        return new DashboardStatistics(
            totalPrograms,
            totalEpisodes,
            unmatchedEpisodeCount,
            missingTranslationCount,
            activeDownloadQueueDepth,
            programsWithMissingEpisodes,
            downloadsCompletedLast7Days);
    }

    private DashboardQueueStatus GetQueueStatus()
    {
        return new DashboardQueueStatus(
            ToQueueInfo(tvdbSeriesLookupNotifier.Items),
            ToQueueInfo(tvdbEpisodeLookupNotifier.Items),
            ToQueueInfo(programRefreshNotifier.Items),
            GetDownloadQueueInfo());
    }

    private static DashboardQueueInfo ToQueueInfo<T>(IReadOnlyList<T> items) where T : IQueueItemSummary
    {
        T? activeItem = items.FirstOrDefault(x => x.IsProcessing);
        return new DashboardQueueInfo(items.Count, activeItem?.ProgramName);
    }

    private DashboardQueueInfo GetDownloadQueueInfo()
    {
        int depth = dbContext.Set<DownloadQueueItem>()
            .Count(x => x.Status == DownloadQueueStatus.Pending || x.Status == DownloadQueueStatus.Downloading);

        string? activeItem = dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Status == DownloadQueueStatus.Downloading)
            .Select(x => x.Episode.Program.Name + " - " + x.Episode.Title)
            .FirstOrDefault();

        return new DashboardQueueInfo(depth, activeItem);
    }
}
