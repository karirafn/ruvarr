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
    public void RendersThreeStatRows()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IReadOnlyList<IElement> rows = cut.FindAll("section.stat-row");
        rows.Count.ShouldBe(3);
        rows[0].ClassList.ShouldContain("stat-row--programs");
        rows[1].ClassList.ShouldContain("stat-row--episodes");
        rows[2].ClassList.ShouldContain("stat-row--downloads");
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
    public void RendersDownloadStatistics()
    {
        // Arrange
        DashboardData data = CreateDashboardData();
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IElement section = cut.Find("section.stat-row--downloads");
        section.QuerySelector("h2")!.TextContent.ShouldBe("Downloads");
        IReadOnlyList<IElement> values = cut.FindAll(".stat-row--downloads .stat-row__card dd");
        values.Count.ShouldBe(4);
        values[0].TextContent.ShouldBe("2");
        values[1].TextContent.ShouldBe("1");
        values[2].TextContent.ShouldBe("7");
        values[3].TextContent.ShouldBe("0");
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
        dls.Count.ShouldBe(3);
        IReadOnlyList<IElement> allCards = cut.FindAll(".stat-row__card");
        allCards.Count.ShouldBe(12);
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
    public void RendersTaskRows()
    {
        // Arrange
        DashboardData data = CreateDashboardData(
            queueStatus: new DashboardQueueStatus(
                new DashboardQueueInfo(3, "Looking up series"),
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(1, null),
                new DashboardQueueInfo(0, null)));
        RegisterHandler(data);
        RegisterBroadcaster();

        // Act
        IRenderedComponent<Ruvarr.Dashboard.Components.Dashboard> cut = Render<Ruvarr.Dashboard.Components.Dashboard>();

        // Assert
        IReadOnlyList<IElement> taskRows = cut.FindAll(".task-row");
        taskRows.Count.ShouldBe(4);

        IElement firstRow = taskRows[0];
        firstRow.QuerySelector(".task-name")!.TextContent.ShouldBe("TVDB Series Lookup");
        firstRow.QuerySelector(".task-depth")!.TextContent.ShouldBe("3");
        firstRow.QuerySelector(".task-active")!.TextContent.ShouldContain("Looking up series");

        IElement secondRow = taskRows[1];
        secondRow.QuerySelector(".task-idle")!.TextContent.ShouldBe("Idle");
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
        DashboardQueueStatus? queueStatus = null)
    {
        return new DashboardData(
            recentlyAdded ?? [],
            requiresTranslation ?? [],
            likelyDownloaded ?? [],
            statistics ?? new DashboardStatistics(
                new ProgramStatistics(10, 4, 6, 1),
                new EpisodeStatistics(50, 30, 5, 3),
                new DownloadStatistics(2, 1, 7, 0)),
            queueStatus ?? new DashboardQueueStatus(
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(0, null),
                new DashboardQueueInfo(0, null)));
    }
}
