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
        result.Statistics.Programs.Total.ShouldBe(0);
        result.Statistics.Programs.Monitored.ShouldBe(0);
        result.Statistics.Programs.Matched.ShouldBe(0);
        result.Statistics.Programs.WithMissingEpisodes.ShouldBe(0);
        result.Statistics.Episodes.Total.ShouldBe(0);
        result.Statistics.Episodes.Matched.ShouldBe(0);
        result.Statistics.Episodes.Unmatched.ShouldBe(0);
        result.Statistics.Episodes.WithoutTranslation.ShouldBe(0);
        result.Statistics.Downloads.QueueDepth.ShouldBe(0);
        result.Statistics.Downloads.Downloading.ShouldBe(0);
        result.Statistics.Downloads.CompletedLast7Days.ShouldBe(0);
        result.Statistics.Downloads.Failed.ShouldBe(0);
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
        program.SetHasMissingEpisodes(true);
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
    public async Task LikelyDownloadedOnceMatched_ExcludesProgramsWithNoMissingEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram programWithMissing = new RuvProgramBuilder()
            .WithRuvId(3101)
            .WithName("Has Missing")
            .WithMultipleEpisodes()
            .Build();
        programWithMissing.MatchTvdb(new TvdbSeriesBuilder().WithName("Has Missing Series").Build());
        programWithMissing.SetMonitoredStatus(true);
        programWithMissing.SetHasMissingEpisodes(true);
        programWithMissing.TryAddEpisode("ep-missing", new Uri("http://test.com"), "Missing Episode", "Desc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram programWithoutMissing = new RuvProgramBuilder()
            .WithRuvId(3102)
            .WithName("No Missing")
            .WithMultipleEpisodes()
            .Build();
        programWithoutMissing.MatchTvdb(new TvdbSeriesBuilder().WithName("No Missing Series").Build());
        programWithoutMissing.SetMonitoredStatus(true);
        programWithoutMissing.TryAddEpisode("ep-no-missing", new Uri("http://test.com"), "No Missing Episode", "Desc", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(programWithMissing, programWithoutMissing);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.LikelyDownloadedOnceMatchedEpisodes.Count.ShouldBe(1);
        result.LikelyDownloadedOnceMatchedEpisodes[0].ProgramName.ShouldBe("Has Missing");
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
        result.Statistics.Programs.Total.ShouldBe(2);
        result.Statistics.Programs.Matched.ShouldBe(1);
        result.Statistics.Programs.WithMissingEpisodes.ShouldBe(1);
        result.Statistics.Episodes.Total.ShouldBe(2);
        result.Statistics.Episodes.Unmatched.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsMonitoredAndMatchedProgramCounts()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram monitoredAndMatched = new RuvProgramBuilder()
            .WithRuvId(6001)
            .WithName("Monitored Matched")
            .WithMultipleEpisodes()
            .Build();
        monitoredAndMatched.MatchTvdb(new TvdbSeriesBuilder().WithName("Series").Build());
        monitoredAndMatched.SetMonitoredStatus(true);

        RuvProgram unmonitoredMatched = new RuvProgramBuilder()
            .WithRuvId(6002)
            .WithName("Unmonitored Matched")
            .WithMultipleEpisodes()
            .Build();
        unmonitoredMatched.MatchTvdb(new TvdbSeriesBuilder().WithName("Series 2").Build());

        RuvProgram unmonitoredUnmatched = new RuvProgramBuilder()
            .WithRuvId(6003)
            .WithName("Unmonitored Unmatched")
            .WithMultipleEpisodes()
            .Build();

        dbContext.Set<RuvProgram>().AddRange(monitoredAndMatched, unmonitoredMatched, unmonitoredUnmatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Statistics.Programs.Total.ShouldBe(3);
        result.Statistics.Programs.Monitored.ShouldBe(1);
        result.Statistics.Programs.Matched.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsDownloadingAndFailedCounts()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(7001)
            .WithName("DL Program")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("DL Series").Build());
        program.TryAddEpisode("dl-downloading", new Uri("http://test.com"), "Downloading Ep", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("dl-failed", new Uri("http://test.com"), "Failed Ep", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode downloadingEp = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "dl-downloading", cancellationToken);
        DownloadQueueItem downloadingItem = DownloadQueueItem.Create(downloadingEp);
        downloadingItem.MarkDownloading();

        RuvEpisode failedEp = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "dl-failed", cancellationToken);
        DownloadQueueItem failedItem = DownloadQueueItem.Create(failedEp);
        failedItem.MarkDownloading();
        failedItem.MarkFailed();

        dbContext.Set<DownloadQueueItem>().AddRange(downloadingItem, failedItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Statistics.Downloads.Downloading.ShouldBe(1);
        result.Statistics.Downloads.Failed.ShouldBe(1);
        result.Statistics.Downloads.QueueDepth.ShouldBe(1);
    }

    [Fact]
    public async Task ReturnsMatchedEpisodeCount()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(8001)
            .WithName("Match Count Program")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("Match Count Series").Build());
        program.TryAddEpisode("mc-matched", new Uri("http://test.com"), "Matched Ep", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("mc-unmatched", new Uri("http://test.com"), "Unmatched Ep", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode matchedEp = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "mc-matched", cancellationToken);
        matchedEp.Match(tvdbId: 200, season: 1, episode: 1, isMissing: false, hasIslTranslation: true);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Statistics.Episodes.Matched.ShouldBe(1);
        result.Statistics.Episodes.Unmatched.ShouldBe(1);
        result.Statistics.Episodes.Total.ShouldBe(2);
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
        result.Statistics.Downloads.CompletedLast7Days.ShouldBe(1);
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
