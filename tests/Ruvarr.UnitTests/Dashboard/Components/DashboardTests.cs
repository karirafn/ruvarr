using AngleSharp.Dom;

using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;

using Shouldly;

namespace Ruvarr.UnitTests.Dashboard.Components;

public sealed class DashboardTests : BunitContext
{
    [Fact]
    public void RendersSpinner_WhenDataIsLoading()
    {
        // Arrange
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            Substitute.For<IRequestHandler<GetDashboardQuery, DashboardData>>();
        handler.Handle(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(new TaskCompletionSource<DashboardData>().Task);
        Services.AddSingleton(handler);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement spinner = cut.Find("div.spinner");
        spinner.ShouldNotBeNull();
        spinner.GetAttribute("role").ShouldBe("status");
        spinner.GetAttribute("aria-label").ShouldBe("Loading");
    }

    [Fact]
    public void RendersTwoStatRows()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IReadOnlyList<IElement> rows = cut.FindAll("section.stat-row");
        rows.Count.ShouldBe(2);
        rows[0].ClassList.ShouldContain("stat-row--programs");
        rows[1].ClassList.ShouldContain("stat-row--episodes");
    }

    [Fact]
    public void RendersProgramStatistics()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section.stat-row--programs");
        section.QuerySelector("h2")!.TextContent.ShouldBe("Programs");
        IReadOnlyList<IElement> values = cut.FindAll(".stat-row--programs .stat-row__card dd");
        values.Count.ShouldBe(4);
        values[0].TextContent.ShouldBe("10");
        values[1].TextContent.ShouldBe("4");
        values[2].TextContent.ShouldBe("6");
        values[3].TextContent.ShouldBe("1");
    }

    [Fact]
    public void RendersEpisodeStatistics()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section.stat-row--episodes");
        section.QuerySelector("h2")!.TextContent.ShouldBe("Episodes");
        IReadOnlyList<IElement> values = cut.FindAll(".stat-row--episodes .stat-row__card dd");
        values.Count.ShouldBe(4);
        values[0].TextContent.ShouldBe("50");
        values[1].TextContent.ShouldBe("30");
        values[2].TextContent.ShouldBe("5");
        values[3].TextContent.ShouldBe("3");
    }

    [Fact]
    public void RendersDownloadCard_WhenIdle()
    {
        // Arrange
        DownloadCardInfo download = new(
            IsDownloading: false,
            ProgramName: null,
            EpisodeTitle: null,
            SeasonEpisodeLabel: null,
            PendingCount: 0,
            BytesDownloaded: null,
            TotalSize: null,
            RateBytesPerSecond: null,
            EstimatedRemaining: null,
            PendingItems: [],
            LastDownloadedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
            CompletedLast7Days: 5,
            FailedCount: 2,
            QueueDepth: 3);
        DashboardData data = CreateDashboardData(download: download);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section.download-card");
        section.ShouldNotBeNull();
        section.QuerySelector(".download-card__badge--idle").ShouldNotBeNull();
        IReadOnlyList<IElement> details = cut.FindAll(".download-card__detail dd");
        details.Count.ShouldBe(4);
        details[1].TextContent.ShouldBe("5");
        details[2].TextContent.ShouldBe("2");
        details[3].TextContent.ShouldBe("3");
    }

    [Fact]
    public void RendersDownloadCard_WhenDownloading()
    {
        // Arrange
        DownloadCardInfo download = new(
            IsDownloading: true,
            ProgramName: "Kastljós",
            EpisodeTitle: "Episode 5",
            SeasonEpisodeLabel: "S01E05",
            PendingCount: 2,
            BytesDownloaded: 5 * 1024 * 1024,
            TotalSize: null,
            RateBytesPerSecond: 1024 * 1024,
            EstimatedRemaining: null,
            PendingItems: [new("Show B", "Episode 2")],
            LastDownloadedAt: null,
            CompletedLast7Days: 0,
            FailedCount: 0,
            QueueDepth: 3);
        DashboardData data = CreateDashboardData(download: download);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section.download-card");
        section.ShouldNotBeNull();
        section.QuerySelector(".download-card__badge--downloading").ShouldNotBeNull();
        section.QuerySelector(".download-card__program-name")!.TextContent.ShouldBe("Kastljós");
    }

    [Fact]
    public void RendersDownloadCard_IndeterminateProgress_WhenTotalSizeIsNull()
    {
        // Arrange
        DownloadCardInfo download = new(
            IsDownloading: true,
            ProgramName: "Show A",
            EpisodeTitle: "Episode 1",
            SeasonEpisodeLabel: null,
            PendingCount: 0,
            BytesDownloaded: 1024,
            TotalSize: null,
            RateBytesPerSecond: null,
            EstimatedRemaining: null,
            PendingItems: [],
            LastDownloadedAt: null,
            CompletedLast7Days: 0,
            FailedCount: 0,
            QueueDepth: 1);
        DashboardData data = CreateDashboardData(download: download);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement progressBar = cut.Find(".download-card__progress-track[role='progressbar']");
        progressBar.GetAttribute("aria-label").ShouldBe("Download in progress (size unknown)");
        progressBar.HasAttribute("aria-valuenow").ShouldBeFalse();
    }

    [Fact]
    public void RendersDownloadCard_NeverState_WhenLastDownloadedAtIsNull()
    {
        // Arrange
        DownloadCardInfo download = new(
            IsDownloading: false,
            ProgramName: null,
            EpisodeTitle: null,
            SeasonEpisodeLabel: null,
            PendingCount: 0,
            BytesDownloaded: null,
            TotalSize: null,
            RateBytesPerSecond: null,
            EstimatedRemaining: null,
            PendingItems: [],
            LastDownloadedAt: null,
            CompletedLast7Days: 0,
            FailedCount: 0,
            QueueDepth: 0);
        DashboardData data = CreateDashboardData(download: download);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        cut.Find("section.download-card").ShouldNotBeNull();
        IReadOnlyList<IElement> details = cut.FindAll(".download-card__detail dd");
        details[0].TextContent.ShouldBe("Never");
    }

    [Fact]
    public void RendersStatCards_AsDefinitionLists()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IReadOnlyList<IElement> dls = cut.FindAll("dl.stat-row__grid");
        dls.Count.ShouldBe(2);
        IReadOnlyList<IElement> allCards = cut.FindAll(".stat-row__card");
        allCards.Count.ShouldBe(8);
    }

    [Fact]
    public void RendersRecentlyAddedEpisodes()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            recentlyAdded: [new("Show A", 100, "Episode 1", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc))]);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Recently Added Episodes']");
        IElement link = section.QuerySelector("a.program-link")!;
        link.GetAttribute("href").ShouldBe("/program/100");
        link.TextContent.ShouldBe("Show A");
        IReadOnlyList<IElement> cells = cut.FindAll("section[aria-label='Recently Added Episodes'] tbody td");
        cells[1].TextContent.ShouldBe("Episode 1");
        cells[2].TextContent.ShouldBe("2026-03-01");
    }

    [Fact]
    public void RendersEmptyMessage_WhenNoRecentlyAddedEpisodes()
    {
        // Arrange
        DashboardData data = CreateDashboardData(recentlyAdded: []);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Recently Added Episodes']");
        section.QuerySelector(".empty-message")!.TextContent.ShouldBe("No recently added episodes.");
    }

    [Fact]
    public void RendersEmptyMessage_WhenNoRequiresTranslationEpisodes()
    {
        // Arrange
        DashboardData data = CreateDashboardData(requiresTranslation: []);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Requires Translation']");
        section.QuerySelector(".empty-message")!.TextContent.ShouldBe("All matched episodes have Icelandic translations.");
    }

    [Fact]
    public void RendersEmptyMessage_WhenNoLikelyDownloadedEpisodes()
    {
        // Arrange
        DashboardData data = CreateDashboardData(likelyDownloaded: []);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Likely Downloaded Once Matched']");
        section.QuerySelector(".empty-message")!.TextContent.ShouldBe("No unmatched episodes on monitored programs.");
    }

    // ── Program Refresh card (slimmed) ─────────────────────────────────────────

    [Fact]
    public void RendersProgramRefreshCard_WhenScheduled_ShowsScheduledBadge()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                LastEnqueuedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastEnqueuedCount: 42,
                NextFireTimeUtc: new DateTimeOffset(2026, 3, 29, 11, 0, 0, TimeSpan.Zero)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        IElement badge = section.QuerySelector(".refresh-card__badge--idle")!;
        badge.ShouldNotBeNull();
        badge.TextContent.Trim().ShouldContain("Scheduled");
    }

    [Fact]
    public void RendersProgramRefreshCard_ShowsThreeDetailLabels()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                LastEnqueuedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastEnqueuedCount: 7,
                NextFireTimeUtc: new DateTimeOffset(2026, 3, 29, 11, 0, 0, TimeSpan.Zero)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        List<IElement> dts = section.QuerySelectorAll(".refresh-card__detail dt").ToList();
        dts.Count.ShouldBe(3);
        dts[0].TextContent.Trim().ShouldBe("Last enqueued");
        dts[1].TextContent.Trim().ShouldBe("Programs queued");
        dts[2].TextContent.Trim().ShouldBe("Next run");
    }

    [Fact]
    public void RendersProgramRefreshCard_ShowsEnqueueValues()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                LastEnqueuedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastEnqueuedCount: 12,
                NextFireTimeUtc: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        List<IElement> dds = section.QuerySelectorAll(".refresh-card__detail dd").ToList();
        dds.Count.ShouldBe(3);
        dds[1].TextContent.Trim().ShouldBe("12");
        dds[2].TextContent.Trim().ShouldBe("—"); // em dash for null NextFireTimeUtc
    }

    [Fact]
    public void RendersProgramRefreshCard_ShowsNever_WhenLastEnqueuedAtIsNull()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                LastEnqueuedAt: null,
                LastEnqueuedCount: null,
                NextFireTimeUtc: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        List<IElement> dds = section.QuerySelectorAll(".refresh-card__detail dd").ToList();
        dds[0].TextContent.Trim().ShouldBe("Never");
        dds[1].TextContent.Trim().ShouldBe("—");
    }

    [Fact]
    public void RendersProgramRefreshCard_AbsenceOfProgressTrack()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        section.QuerySelector(".refresh-card__progress-track").ShouldBeNull();
    }

    // ── Episode Sync card ───────────────────────────────────────────────────────

    [Fact]
    public void RendersEpisodeSyncCard_WhenRunning()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 33,
                CompletedCount: 12,
                CurrentProgram: "Kastljós",
                LastCompletedAt: null,
                LastRunDuration: TimeSpan.FromMinutes(4),
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        section.QuerySelector(".episode-sync-card__badge--running").ShouldNotBeNull();
        section.QuerySelector(".episode-sync-card__progress-track").ShouldNotBeNull();
        IElement count = section.QuerySelector(".episode-sync-card__count")!;
        count.ShouldNotBeNull();
        count.TextContent.Trim().ShouldBe("12 / 45");
        IElement progressBar = section.QuerySelector("[role='progressbar']")!;
        progressBar.GetAttribute("aria-valuenow").ShouldBe("26");
        progressBar.GetAttribute("aria-valuemin").ShouldBe("0");
        progressBar.GetAttribute("aria-valuemax").ShouldBe("100");
    }

    [Fact]
    public void RendersEpisodeSyncCard_WhenRunning_ShowsCurrentProgram()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 10,
                CompletedCount: 5,
                CurrentProgram: "Fréttir",
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        section.QuerySelector(".episode-sync-card__current")!.TextContent.Trim().ShouldBe("Fréttir");
    }

    [Fact]
    public void RendersEpisodeSyncCard_WhenIdle()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: false,
                Depth: 0,
                CompletedCount: 0,
                CurrentProgram: null,
                LastCompletedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastRunDuration: TimeSpan.FromSeconds(45),
                LastRunTotal: 40,
                NextFireTimeUtc: new DateTimeOffset(2026, 3, 29, 11, 0, 0, TimeSpan.Zero),
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        section.QuerySelector(".episode-sync-card__badge--idle").ShouldNotBeNull();
        section.QuerySelector(".episode-sync-card__progress-track").ShouldBeNull();
        List<IElement> dts = section.QuerySelectorAll(".episode-sync-card__detail dt").ToList();
        dts.Count.ShouldBe(4);
        dts[0].TextContent.Trim().ShouldBe("Last completed");
        dts[1].TextContent.Trim().ShouldBe("Duration");
        dts[2].TextContent.Trim().ShouldBe("Episodes");
        dts[3].TextContent.Trim().ShouldBe("Next run");
        List<IElement> dds = section.QuerySelectorAll(".episode-sync-card__detail dd").ToList();
        dds[2].TextContent.Trim().ShouldBe("40");
    }

    [Fact]
    public void RendersEpisodeSyncCard_WhenIdle_ShowsNever_WhenLastCompletedAtIsNull()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: false,
                Depth: 0,
                CompletedCount: 0,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        List<IElement> dds = section.QuerySelectorAll(".episode-sync-card__detail dd").ToList();
        dds[0].TextContent.Trim().ShouldBe("Never");
    }

    [Fact]
    public void WhenStalled_StallBannerContainerPresentWithTextAndIcon()
    {
        // Arrange
        TimeSpan stalledFor = TimeSpan.FromHours(3) + TimeSpan.FromMinutes(12);
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: false,
                Depth: 0,
                CompletedCount: 5,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: stalledFor));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert — live region container must be present; inner content shows stall text + icon
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement alertContainer = section.QuerySelector("[role='alert']").ShouldNotBeNull();
        alertContainer.QuerySelector(".episode-sync-card__stall-text").ShouldNotBeNull();
        IElement stallText = alertContainer.QuerySelector(".episode-sync-card__stall-text")!;
        stallText.TextContent.ShouldContain("Stalled");
        stallText.TextContent.ShouldContain("no completion for");
        stallText.TextContent.ShouldContain("3h 12m");
        // Warning icon svg must be rendered inside the alert container
        alertContainer.QuerySelector("svg").ShouldNotBeNull();
    }

    [Fact]
    public void WhenNotStalled_StallBannerContainerPresentButTextAndIconAbsent()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: false,
                Depth: 0,
                CompletedCount: 0,
                CurrentProgram: null,
                LastCompletedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert — live region container is permanently mounted (always in DOM for screen readers)
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement alertContainer = section.QuerySelector("[role='alert']").ShouldNotBeNull();
        // Inner content absent: no stall text or warning icon
        alertContainer.QuerySelector(".episode-sync-card__stall-text").ShouldBeNull();
        alertContainer.QuerySelector("svg").ShouldBeNull();
    }

    [Fact]
    public void WhenRunning_ProgressBar_HasAriaValueText_WithEpisodeFraction()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 33,
                CompletedCount: 12,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement progressBar = section.QuerySelector("[role='progressbar']").ShouldNotBeNull();
        progressBar.GetAttribute("aria-valuetext").ShouldBe("12 / 45 episodes");
    }

    [Fact]
    public void WhenRunningAndStalled_ProgressBar_AriaValueText_HasStalledPrefix()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 20,
                CompletedCount: 5,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: TimeSpan.FromHours(2)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement progressBar = section.QuerySelector("[role='progressbar']").ShouldNotBeNull();
        progressBar.GetAttribute("aria-valuetext").ShouldBe("Stalled — 5 / 25 episodes");
    }

    [Fact]
    public void WhenRunningAndStalled_ProgressBar_HasStalledModifierClass()
    {
        // Arrange — locks in #409: animated stripe must not convey motion while stalled
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 20,
                CompletedCount: 5,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: TimeSpan.FromHours(2)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement bar = section.QuerySelector(".episode-sync-card__progress-bar").ShouldNotBeNull();
        bar.ClassList.ShouldContain("episode-sync-card__progress-bar--stalled");
    }

    [Fact]
    public void WhenRunningAndNotStalled_ProgressBar_LacksStalledModifierClass()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 20,
                CompletedCount: 5,
                CurrentProgram: null,
                LastCompletedAt: null,
                LastRunDuration: null,
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        IElement bar = section.QuerySelector(".episode-sync-card__progress-bar").ShouldNotBeNull();
        bar.ClassList.ShouldNotContain("episode-sync-card__progress-bar--stalled");
    }

    [Fact]
    public void RendersEpisodeSyncCard_WhenStalled_EtaIsSuppressed()
    {
        // Arrange — stalled while running (wedged queue scenario)
        DashboardData data = CreateDashboardData(
            episodeSync: new EpisodeSyncCardInfo(
                IsRunning: true,
                Depth: 20,
                CompletedCount: 5,
                CurrentProgram: "Kastljós",
                LastCompletedAt: null,
                LastRunDuration: TimeSpan.FromMinutes(10),
                LastRunTotal: null,
                NextFireTimeUtc: null,
                StalledFor: TimeSpan.FromHours(3)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert — progress bar still visible (count framing), but ETA is absent
        IElement section = cut.Find("section[aria-label='Episode Sync']");
        section.QuerySelector(".episode-sync-card__progress-track").ShouldNotBeNull();
        section.QuerySelector(".episode-sync-card__eta").ShouldBeNull();
    }

    // ── TVDB cards ─────────────────────────────────────────────────────────────

    [Fact]
    public void RendersTvdbSeriesLookupCard_WhenIdle()
    {
        // Arrange
        TvdbSeriesLookupCardInfo tvdbSeriesLookup = new(
            IsProcessing: false,
            CurrentProgram: null,
            PendingCount: 3,
            LastLookedUpAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
            RetryCount: 5);
        DashboardData data = CreateDashboardData(tvdbSeriesLookup: tvdbSeriesLookup);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='TVDB Series Lookup']");
        section.ShouldNotBeNull();
        section.QuerySelector(".tvdb-lookup-card__badge--idle").ShouldNotBeNull();
        List<IElement> details = section.QuerySelectorAll(".tvdb-lookup-card__detail dd").ToList();
        details.Count.ShouldBe(3);
        details[1].TextContent.ShouldBe("3");
        details[2].TextContent.ShouldBe("5");
    }

    [Fact]
    public void RendersTvdbSeriesLookupCard_WhenProcessing()
    {
        // Arrange
        TvdbSeriesLookupCardInfo tvdbSeriesLookup = new(
            IsProcessing: true,
            CurrentProgram: "Kastljos",
            PendingCount: 2,
            LastLookedUpAt: null,
            RetryCount: 0);
        DashboardData data = CreateDashboardData(tvdbSeriesLookup: tvdbSeriesLookup);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='TVDB Series Lookup']");
        section.ShouldNotBeNull();
        section.QuerySelector(".tvdb-lookup-card__badge--processing").ShouldNotBeNull();
        section.QuerySelector(".tvdb-lookup-card__current-program")!.TextContent.ShouldBe("Kastljos");
    }

    [Fact]
    public void RendersTvdbSeriesLookupCard_NeverState_WhenLastLookedUpAtIsNull()
    {
        // Arrange
        TvdbSeriesLookupCardInfo tvdbSeriesLookup = new(
            IsProcessing: false,
            CurrentProgram: null,
            PendingCount: 0,
            LastLookedUpAt: null,
            RetryCount: 0);
        DashboardData data = CreateDashboardData(tvdbSeriesLookup: tvdbSeriesLookup);
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='TVDB Series Lookup']");
        section.ShouldNotBeNull();
        List<IElement> details = section.QuerySelectorAll(".tvdb-lookup-card__detail dd").ToList();
        details[0].TextContent.ShouldBe("Never");
    }

    [Fact]
    public void RendersHeading()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        cut.Find("h1").TextContent.ShouldBe("Dashboard");
    }

    private void RegisterHandler(DashboardData data)
    {
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            Substitute.For<IRequestHandler<GetDashboardQuery, DashboardData>>();
        handler.Handle(Arg.Any<GetDashboardQuery>(), Arg.Any<CancellationToken>())
            .Returns(data);
        Services.AddSingleton(handler);
    }

    private void RegisterBroadcaster()
    {
        IDomainEventBroadcaster broadcaster = Substitute.For<IDomainEventBroadcaster>();
        Services.AddSingleton(broadcaster);
    }

    private static DashboardData CreateDashboardData(
        IReadOnlyList<DashboardEpisodeItem>? recentlyAdded = null,
        IReadOnlyList<DashboardEpisodeItem>? requiresTranslation = null,
        IReadOnlyList<DashboardEpisodeItem>? likelyDownloaded = null,
        DashboardStatistics? statistics = null,
        DashboardQueueStatus? queueStatus = null,
        ProgramRefreshCardInfo? programRefresh = null,
        EpisodeSyncCardInfo? episodeSync = null,
        TvdbSeriesLookupCardInfo? tvdbSeriesLookup = null,
        TvdbEpisodeLookupCardInfo? tvdbEpisodeLookup = null,
        DownloadCardInfo? download = null)
    {
        return new DashboardData(
            recentlyAdded ?? [],
            requiresTranslation ?? [],
            likelyDownloaded ?? [],
            statistics ?? new DashboardStatistics(
                new ProgramStatistics(10, 4, 6, 1),
                new EpisodeStatistics(50, 30, 5, 3)),
            queueStatus ?? new DashboardQueueStatus(
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(0, null)),
            programRefresh ?? DefaultProgramRefreshCard,
            episodeSync ?? DefaultEpisodeSyncCard,
            tvdbSeriesLookup ?? DefaultTvdbSeriesLookupCard,
            tvdbEpisodeLookup ?? DefaultTvdbEpisodeLookupCard,
            download ?? DefaultDownloadCard);
    }

    private static readonly ProgramRefreshCardInfo DefaultProgramRefreshCard = new(
        LastEnqueuedAt: null,
        LastEnqueuedCount: null,
        NextFireTimeUtc: null);

    private static readonly EpisodeSyncCardInfo DefaultEpisodeSyncCard = new(
        IsRunning: false,
        Depth: 0,
        CompletedCount: 0,
        CurrentProgram: null,
        LastCompletedAt: null,
        LastRunDuration: null,
        LastRunTotal: null,
        NextFireTimeUtc: null,
        StalledFor: null);

    private static readonly TvdbSeriesLookupCardInfo DefaultTvdbSeriesLookupCard = new(
        IsProcessing: false,
        CurrentProgram: null,
        PendingCount: 0,
        LastLookedUpAt: null,
        RetryCount: 0);

    private static readonly TvdbEpisodeLookupCardInfo DefaultTvdbEpisodeLookupCard = new(
        IsProcessing: false,
        CurrentProgram: null,
        PendingCount: 0,
        LastLookedUpAt: null,
        RetryCount: 0);

    private static readonly DownloadCardInfo DefaultDownloadCard = new(
        IsDownloading: false,
        ProgramName: null,
        EpisodeTitle: null,
        SeasonEpisodeLabel: null,
        PendingCount: 0,
        BytesDownloaded: null,
        TotalSize: null,
        RateBytesPerSecond: null,
        EstimatedRemaining: null,
        PendingItems: [],
        LastDownloadedAt: null,
        CompletedLast7Days: 0,
        FailedCount: 0,
        QueueDepth: 0);
}
