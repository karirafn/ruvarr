namespace Ruvarr.Dashboard;

internal sealed record DashboardData(
    IReadOnlyList<DashboardEpisodeItem> RecentlyAddedEpisodes,
    IReadOnlyList<DashboardEpisodeItem> RequiresTranslationEpisodes,
    IReadOnlyList<DashboardEpisodeItem> LikelyDownloadedOnceMatchedEpisodes,
    DashboardStatistics Statistics,
    DashboardQueueStatus QueueStatus,
    ProgramRefreshCardInfo ProgramRefresh);

internal sealed record DashboardEpisodeItem(
    string ProgramName,
    int ProgramRuvId,
    string EpisodeTitle,
    DateTime FirstRun);

internal sealed record DashboardStatistics(
    ProgramStatistics Programs,
    EpisodeStatistics Episodes,
    DownloadStatistics Downloads);

internal sealed record ProgramStatistics(
    int Total,
    int Monitored,
    int Matched,
    int WithMissingEpisodes);

internal sealed record EpisodeStatistics(
    int Total,
    int Matched,
    int Unmatched,
    int WithoutTranslation);

internal sealed record DownloadStatistics(
    int QueueDepth,
    int Downloading,
    int CompletedLast7Days,
    int Failed);

internal sealed record DashboardQueueStatus(
    DashboardQueueInfo TvdbSeriesLookup,
    DashboardQueueInfo TvdbEpisodeLookup,
    DashboardQueueInfo Download);

internal sealed record DashboardQueueInfo(
    int Depth,
    string? ActiveItem);

internal sealed record ProgramRefreshCardInfo(
    bool IsRunning,
    int Depth,
    int CompletedCount,
    string? CurrentProgram,
    DateTimeOffset? LastCompletedAt,
    TimeSpan? LastRunDuration,
    int? LastRunTotal,
    DateTimeOffset? NextFireTimeUtc);
