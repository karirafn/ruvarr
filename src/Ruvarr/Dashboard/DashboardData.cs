namespace Ruvarr.Dashboard;

internal sealed record DashboardData(
    IReadOnlyList<DashboardRecentlyAddedItem> RecentlyAddedEpisodes,
    IReadOnlyList<DashboardEpisodeItem> RequiresTranslationEpisodes,
    IReadOnlyList<DashboardEpisodeItem> LikelyDownloadedOnceMatchedEpisodes,
    DashboardStatistics Statistics,
    DashboardQueueStatus QueueStatus,
    ProgramRefreshCardInfo ProgramRefresh,
    EpisodeSyncCardInfo EpisodeSync,
    TvdbSeriesLookupCardInfo TvdbSeriesLookup,
    TvdbEpisodeLookupCardInfo TvdbEpisodeLookup,
    DownloadCardInfo Download);

internal sealed record DashboardRecentlyAddedItem(
    string ProgramName,
    int ProgramRuvId,
    string? EpisodeTitle,
    int EpisodeCount,
    DateTime Added,
    bool IsMatched);

internal sealed record DashboardEpisodeItem(
    string ProgramName,
    int ProgramRuvId,
    string EpisodeTitle,
    DateTime FirstRun);

internal sealed record DashboardStatistics(
    ProgramStatistics Programs,
    EpisodeStatistics Episodes);

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

internal sealed record DashboardQueueStatus(
    DashboardQueueInfo TvdbSeriesLookup,
    DashboardQueueInfo TvdbEpisodeLookup,
    DashboardQueueInfo Download);

internal sealed record DashboardQueueInfo(
    int Depth,
    string? ActiveItem);

internal sealed record ProgramRefreshCardInfo(
    DateTimeOffset? LastEnqueuedAt,
    int? LastEnqueuedCount,
    DateTimeOffset? NextFireTimeUtc);

internal sealed record EpisodeSyncCardInfo(
    bool IsRunning,
    int Depth,
    int CompletedCount,
    string? CurrentProgram,
    DateTimeOffset? LastCompletedAt,
    TimeSpan? LastRunDuration,
    int? LastRunTotal,
    DateTimeOffset? NextFireTimeUtc,
    TimeSpan? StalledFor);

internal sealed record DownloadCardInfo(
    bool IsDownloading,
    string? ProgramName,
    string? EpisodeTitle,
    string? SeasonEpisodeLabel,
    int PendingCount,
    long? BytesDownloaded,
    long? TotalSize,
    double? RateBytesPerSecond,
    TimeSpan? EstimatedRemaining,
    IReadOnlyList<DownloadCardPendingItem> PendingItems,
    DateTimeOffset? LastDownloadedAt,
    int CompletedLast7Days,
    int FailedCount,
    int QueueDepth);

internal sealed record TvdbSeriesLookupCardInfo(
    bool IsProcessing,
    string? CurrentProgram,
    int PendingCount,
    DateTimeOffset? LastLookedUpAt,
    int RetryCount);

internal sealed record TvdbEpisodeLookupCardInfo(
    bool IsProcessing,
    string? CurrentProgram,
    int PendingCount,
    DateTimeOffset? LastLookedUpAt,
    int RetryCount);

internal sealed record DownloadCardPendingItem(string ProgramName, string EpisodeTitle);
