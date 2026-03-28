namespace Ruvarr.Dashboard;

internal sealed record DashboardData(
    IReadOnlyList<DashboardEpisodeItem> RecentlyAddedEpisodes,
    IReadOnlyList<DashboardEpisodeItem> RequiresTranslationEpisodes,
    IReadOnlyList<DashboardEpisodeItem> LikelyDownloadedOnceMatchedEpisodes,
    DashboardStatistics Statistics,
    DashboardQueueStatus QueueStatus);

internal sealed record DashboardEpisodeItem(
    string ProgramName,
    int ProgramRuvId,
    string EpisodeTitle,
    DateTime FirstRun);

internal sealed record DashboardStatistics(
    int TotalPrograms,
    int TotalEpisodes,
    int UnmatchedEpisodeCount,
    int MissingTranslationCount,
    int ActiveDownloadQueueDepth,
    int ProgramsWithMissingEpisodes,
    int DownloadsCompletedLast7Days);

internal sealed record DashboardQueueStatus(
    DashboardQueueInfo TvdbSeriesLookup,
    DashboardQueueInfo TvdbEpisodeLookup,
    DashboardQueueInfo ProgramRefresh,
    DashboardQueueInfo Download);

internal sealed record DashboardQueueInfo(
    int Depth,
    string? ActiveItem);
