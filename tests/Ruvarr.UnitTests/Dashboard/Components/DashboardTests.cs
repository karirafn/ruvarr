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

    [Fact]
    public void RendersProgramRefreshCard_WhenRunning()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                IsRunning: true,
                Depth: 33,
                CompletedCount: 12,
                CurrentProgram: "Kastljós",
                LastCompletedAt: null,
                LastRunDuration: TimeSpan.FromMinutes(4),
                LastRunTotal: null,
                NextFireTimeUtc: null));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        section.QuerySelector(".refresh-card__badge--running").ShouldNotBeNull();
        section.QuerySelector(".refresh-card__count")!.TextContent.ShouldBe("12 / 45");
        section.QuerySelector(".refresh-card__current")!.TextContent.ShouldBe("Kastljós");
        IElement progressBar = section.QuerySelector("[role='progressbar']")!;
        progressBar.GetAttribute("aria-valuenow").ShouldBe("26");
        progressBar.GetAttribute("aria-valuemin").ShouldBe("0");
        progressBar.GetAttribute("aria-valuemax").ShouldBe("100");
    }

    [Fact]
    public void RendersProgramRefreshCard_WhenIdle()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            programRefresh: new ProgramRefreshCardInfo(
                IsRunning: false,
                Depth: 0,
                CompletedCount: 0,
                CurrentProgram: null,
                LastCompletedAt: new DateTimeOffset(2026, 3, 29, 10, 0, 0, TimeSpan.Zero),
                LastRunDuration: TimeSpan.FromSeconds(45),
                LastRunTotal: 40,
                NextFireTimeUtc: new DateTimeOffset(2026, 3, 29, 11, 0, 0, TimeSpan.Zero)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section[aria-label='Program Refresh']");
        section.QuerySelector(".refresh-card__badge--idle").ShouldNotBeNull();
        IReadOnlyList<IElement> details = cut.FindAll(".refresh-card__detail dd");
        details.Count.ShouldBe(4);
        details[2].TextContent.ShouldBe("40");
    }

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
        IElement section = cut.Find("section.tvdb-lookup-card");
        section.ShouldNotBeNull();
        section.QuerySelector(".tvdb-lookup-card__badge--idle").ShouldNotBeNull();
        IReadOnlyList<IElement> details = cut.FindAll(".tvdb-lookup-card__detail dd");
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
        IElement section = cut.Find("section.tvdb-lookup-card");
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
        cut.Find("section.tvdb-lookup-card").ShouldNotBeNull();
        IReadOnlyList<IElement> details = cut.FindAll(".tvdb-lookup-card__detail dd");
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
        TvdbSeriesLookupCardInfo? tvdbSeriesLookup = null,
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
            programRefresh ?? new ProgramRefreshCardInfo(
                false, 0, 0, null, null, null, null, null),
            tvdbSeriesLookup ?? DefaultTvdbSeriesLookupCard,
            download ?? DefaultDownloadCard);
    }

    private static readonly TvdbSeriesLookupCardInfo DefaultTvdbSeriesLookupCard = new(
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
