using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Dashboard.Queries;

public sealed class GetDashboardHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ReturnsEmptyDashboard_WhenNoData()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.ShouldBeEmpty();
        result.RequiresTranslationEpisodes.ShouldBeEmpty();
        result.LikelyDownloadedOnceMatchedEpisodes.ShouldBeEmpty();
        result.Statistics.TotalPrograms.ShouldBe(0);
        result.Statistics.TotalEpisodes.ShouldBe(0);
        result.Statistics.UnmatchedEpisodeCount.ShouldBe(0);
        result.Statistics.MissingTranslationCount.ShouldBe(0);
        result.Statistics.ActiveDownloadQueueDepth.ShouldBe(0);
        result.Statistics.ProgramsWithMissingEpisodes.ShouldBe(0);
        result.Statistics.DownloadsCompletedLast7Days.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsRecentlyAddedEpisodes_ForMatchedPrograms()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram matchedProgram = new RuvProgramBuilder()
            .WithRuvId(1001)
            .WithName("Matched Show")
            .WithMultipleEpisodes()
            .Build();
        matchedProgram.MatchTvdb(new TvdbSeriesBuilder().WithName("Matched Series").Build());
        matchedProgram.TryAddEpisode("ep1", new Uri("http://test.com"), "Episode 1", "Desc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram unmatchedProgram = new RuvProgramBuilder()
            .WithRuvId(1002)
            .WithName("Unmatched Show")
            .WithMultipleEpisodes()
            .Build();
        unmatchedProgram.TryAddEpisode("ep2", new Uri("http://test.com"), "Episode 2", "Desc", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(matchedProgram, unmatchedProgram);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(1);
        result.RecentlyAddedEpisodes[0].ProgramName.ShouldBe("Matched Show");
        result.RecentlyAddedEpisodes[0].EpisodeTitle.ShouldBe("Episode 1");
    }

    [Fact]
    public async Task ReturnsRequiresTranslationEpisodes_WhenNoIslTranslation()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(2001)
            .WithName("Translation Show")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("Translation Series").Build());
        program.TryAddEpisode("ep-no-isl", new Uri("http://test.com"), "No Translation", "Desc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-has-isl", new Uri("http://test.com"), "Has Translation", "Desc", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode epNoIsl = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-no-isl", cancellationToken);
        epNoIsl.Match(tvdbId: 100, season: 1, episode: 1, isMissing: false, hasIslTranslation: false);

        RuvEpisode epHasIsl = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-has-isl", cancellationToken);
        epHasIsl.Match(tvdbId: 101, season: 1, episode: 2, isMissing: false, hasIslTranslation: true);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RequiresTranslationEpisodes.Count.ShouldBe(1);
        result.RequiresTranslationEpisodes[0].EpisodeTitle.ShouldBe("No Translation");
    }

    [Fact]
    public async Task ReturnsLikelyDownloadedOnceMatched_ExcludesGenericTitles()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(3001)
            .WithName("Monitored Show")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("Monitored Series").Build());
        program.SetMonitoredStatus(true);
        program.TryAddEpisode("ep-named", new Uri("http://test.com"), "Named Episode", "Desc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-generic", new Uri("http://test.com"), "Þáttur 4 af 6", "Desc", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.LikelyDownloadedOnceMatchedEpisodes.Count.ShouldBe(1);
        result.LikelyDownloadedOnceMatchedEpisodes[0].EpisodeTitle.ShouldBe("Named Episode");
    }

    [Fact]
    public async Task ReturnsCorrectStatistics()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program1 = new RuvProgramBuilder()
            .WithRuvId(4001)
            .WithName("Program 1")
            .WithMultipleEpisodes()
            .Build();
        program1.MatchTvdb(new TvdbSeriesBuilder().WithName("Series 1").Build());
        program1.SetHasMissingEpisodes(true);
        program1.TryAddEpisode("ep1", new Uri("http://test.com"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        RuvProgram program2 = new RuvProgramBuilder()
            .WithRuvId(4002)
            .WithName("Program 2")
            .WithMultipleEpisodes()
            .Build();
        program2.TryAddEpisode("ep2", new Uri("http://test.com"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(program1, program2);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Statistics.TotalPrograms.ShouldBe(2);
        result.Statistics.TotalEpisodes.ShouldBe(2);
        result.Statistics.UnmatchedEpisodeCount.ShouldBe(2);
        result.Statistics.ProgramsWithMissingEpisodes.ShouldBe(1);
    }

    [Fact]
    public async Task CountsDownloadsCompletedInLast7Days()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(5001)
            .WithName("Download Program")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("Download Series").Build());
        program.TryAddEpisode("dl-ep1", new Uri("http://test.com"), "Download Ep 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "dl-ep1", cancellationToken);
        DownloadQueueItem downloadItem = DownloadQueueItem.Create(episode);
        downloadItem.MarkDownloading();
        downloadItem.MarkDownloaded();
        dbContext.Set<DownloadQueueItem>().Add(downloadItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Statistics.DownloadsCompletedLast7Days.ShouldBe(1);
    }

    [Fact]
    public async Task ReturnsQueueStatus()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.QueueStatus.ShouldNotBeNull();
        result.QueueStatus.TvdbSeriesLookup.ShouldNotBeNull();
        result.QueueStatus.TvdbEpisodeLookup.ShouldNotBeNull();
        result.QueueStatus.ProgramRefresh.ShouldNotBeNull();
        result.QueueStatus.Download.ShouldNotBeNull();
        result.QueueStatus.TvdbSeriesLookup.Depth.ShouldBeGreaterThanOrEqualTo(0);
        result.QueueStatus.TvdbEpisodeLookup.Depth.ShouldBeGreaterThanOrEqualTo(0);
        result.QueueStatus.ProgramRefresh.Depth.ShouldBeGreaterThanOrEqualTo(0);
        result.QueueStatus.Download.Depth.ShouldBeGreaterThanOrEqualTo(0);
    }
}
