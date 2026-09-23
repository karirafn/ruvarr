using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Ruvarr.Abstractions;
using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;
using Ruvarr.Downloads.Domain;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Time;

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
        result.Download.QueueDepth.ShouldBe(0);
        result.Download.PendingCount.ShouldBe(0);
        result.Download.CompletedLast7Days.ShouldBe(0);
        result.Download.FailedCount.ShouldBe(0);
    }

    [Fact]
    public async Task WhenEpisodesWithinWindow_ReturnsAllProgramsOrderedByMostRecentIngest()
    {
        // Arrange: two programs (one matched, one unmatched) both within the 7-day window.
        // The unmatched program's episode has the more-recent Created timestamp.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram matchedProgram = new RuvProgramBuilder()
            .WithRuvId(1001)
            .WithName("Matched Show")
            .WithMultipleEpisodes()
            .Build();
        matchedProgram.MatchTvdb(new TvdbSeriesBuilder().WithName("Matched Series").Build());
        matchedProgram.TryAddEpisode("ep1", new Uri("http://test.com"), "Matched Episode", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram unmatchedProgram = new RuvProgramBuilder()
            .WithRuvId(1002)
            .WithName("Unmatched Show")
            .WithMultipleEpisodes()
            .Build();
        unmatchedProgram.TryAddEpisode("ep2", new Uri("http://test.com"), "Unmatched Episode", "Desc", new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(matchedProgram, unmatchedProgram);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Backdate Created to within the 7-day window (domain path cannot backdate ingest time).
        RuvEpisode ep1 = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep1", cancellationToken);
        RuvEpisode ep2 = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep2", cancellationToken);
        dbContext.Entry(ep1).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-3);
        dbContext.Entry(ep2).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-1);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert: both programs appear; unmatched comes first (more recent ingest)
        result.RecentlyAddedEpisodes.Count.ShouldBe(2);
        result.RecentlyAddedEpisodes[0].ProgramName.ShouldBe("Unmatched Show");
        result.RecentlyAddedEpisodes[1].ProgramName.ShouldBe("Matched Show");
    }

    [Fact]
    public async Task WhenExactlyOneWindowedEpisode_EpisodeTitleSetAndCountIsOne()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1101)
            .WithName("Single Episode Show")
            .WithMultipleEpisodes()
            .Build();
        program.TryAddEpisode("ep-single", new Uri("http://test.com"), "The One Episode", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-single", cancellationToken);
        dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-2);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(1);
        DashboardRecentlyAddedItem item = result.RecentlyAddedEpisodes[0];
        item.ShouldSatisfyAllConditions(
            () => item.EpisodeTitle.ShouldBe("The One Episode"),
            () => item.EpisodeCount.ShouldBe(1));
    }

    [Fact]
    public async Task WhenMultipleWindowedEpisodes_EpisodeTitleNullAndCountIsN()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1201)
            .WithName("Multi Episode Show")
            .WithMultipleEpisodes()
            .Build();
        program.TryAddEpisode("ep-multi-1", new Uri("http://test.com"), "Episode A", "Desc", new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-multi-2", new Uri("http://test.com"), "Episode B", "Desc", new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-multi-3", new Uri("http://test.com"), "Episode C", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (string ruvId in new[] { "ep-multi-1", "ep-multi-2", "ep-multi-3" })
        {
            RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == ruvId, cancellationToken);
            dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-4);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(1);
        DashboardRecentlyAddedItem item = result.RecentlyAddedEpisodes[0];
        item.ShouldSatisfyAllConditions(
            () => item.EpisodeTitle.ShouldBeNull(),
            () => item.EpisodeCount.ShouldBe(3));
    }

    [Fact]
    public async Task WhenNoWindowedEpisodes_ReturnsEmptyList()
    {
        // Arrange: episode Created is outside the 7-day window
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1301)
            .WithName("Old Show")
            .WithMultipleEpisodes()
            .Build();
        program.TryAddEpisode("ep-old", new Uri("http://test.com"), "Old Episode", "Desc", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Set Created to 8 days ago (outside the 7-day window)
        RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-old", cancellationToken);
        dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-8);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenMatchedProgram_IsMatchedTrue_WhenUnmatched_IsMatchedFalse()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram matchedProgram = new RuvProgramBuilder()
            .WithRuvId(1401)
            .WithName("Matched Program")
            .WithMultipleEpisodes()
            .Build();
        matchedProgram.MatchTvdb(new TvdbSeriesBuilder().WithName("Some Series").Build());
        matchedProgram.TryAddEpisode("ep-matched", new Uri("http://test.com"), "Matched Ep", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram unmatchedProgram = new RuvProgramBuilder()
            .WithRuvId(1402)
            .WithName("Unmatched Program")
            .WithMultipleEpisodes()
            .Build();
        unmatchedProgram.TryAddEpisode("ep-unmatched", new Uri("http://test.com"), "Unmatched Ep", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(matchedProgram, unmatchedProgram);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (string ruvId in new[] { "ep-matched", "ep-unmatched" })
        {
            RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == ruvId, cancellationToken);
            dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-2);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        DashboardRecentlyAddedItem matched = result.RecentlyAddedEpisodes.Single(x => x.ProgramName == "Matched Program");
        DashboardRecentlyAddedItem unmatched = result.RecentlyAddedEpisodes.Single(x => x.ProgramName == "Unmatched Program");
        matched.IsMatched.ShouldBeTrue();
        unmatched.IsMatched.ShouldBeFalse();
    }

    [Fact]
    public async Task WhenOneProgramWithSeveralWindowedEpisodes_ReturnsOneRowWithCount()
    {
        // Arrange: back-catalogue burst. One program with many windowed episodes produces one grouped row.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1501)
            .WithName("Burst Show")
            .WithMultipleEpisodes()
            .Build();

        string[] ruvIds = ["burst-1", "burst-2", "burst-3", "burst-4", "burst-5"];
        foreach (string id in ruvIds)
        {
            program.TryAddEpisode(id, new Uri("http://test.com"), $"Episode {id}", "Desc", new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        }

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (string id in ruvIds)
        {
            RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == id, cancellationToken);
            dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-3);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(1);
        result.RecentlyAddedEpisodes[0].EpisodeCount.ShouldBe(5);
        result.RecentlyAddedEpisodes[0].EpisodeTitle.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTwoProgramsBothWithinWindow_ReturnsTwoRows()
    {
        // Arrange: two distinct programs with windowed episodes produce two rows
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram programA = new RuvProgramBuilder()
            .WithRuvId(1601)
            .WithName("Sign Language A")
            .WithMultipleEpisodes()
            .Build();
        programA.TryAddEpisode("sl-a-ep", new Uri("http://test.com"), "Episode A", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram programB = new RuvProgramBuilder()
            .WithRuvId(1602)
            .WithName("Sign Language B")
            .WithMultipleEpisodes()
            .Build();
        programB.TryAddEpisode("sl-b-ep", new Uri("http://test.com"), "Episode B", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(programA, programB);
        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (string id in new[] { "sl-a-ep", "sl-b-ep" })
        {
            RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == id, cancellationToken);
            dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-2);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(2);
        result.RecentlyAddedEpisodes.ShouldContain(x => x.ProgramName == "Sign Language A");
        result.RecentlyAddedEpisodes.ShouldContain(x => x.ProgramName == "Sign Language B");
    }

    [Fact]
    public async Task WhenEpisodeCreatedEqualsWindowCutoff_IsIncluded_WhenOneTickEarlier_IsExcluded()
    {
        // Arrange: one episode at exactly the cutoff (included) and one one tick before it (excluded).
        // RuvEpisode.Create stamps Created = now and cannot backdate ingest time,
        // so past Created values are set directly via DbContext (same approach as sibling window tests).
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        DateTime cutoff = now.UtcDateTime - TimeSpan.FromDays(GetDashboardHandler.RecentlyAddedWindowDays);

        RuvProgram atBoundaryProgram = new RuvProgramBuilder()
            .WithRuvId(1801)
            .WithName("At Boundary Show")
            .WithMultipleEpisodes()
            .Build();
        atBoundaryProgram.TryAddEpisode("ep-at-boundary", new Uri("http://test.com"), "Boundary Episode", "Desc", new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        RuvProgram beforeBoundaryProgram = new RuvProgramBuilder()
            .WithRuvId(1802)
            .WithName("Before Boundary Show")
            .WithMultipleEpisodes()
            .Build();
        beforeBoundaryProgram.TryAddEpisode("ep-before-boundary", new Uri("http://test.com"), "Before Boundary Episode", "Desc", new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().AddRange(atBoundaryProgram, beforeBoundaryProgram);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode epAtBoundary = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-at-boundary", cancellationToken);
        dbContext.Entry(epAtBoundary).Property(nameof(RuvEpisode.Created)).CurrentValue = cutoff;

        RuvEpisode epBeforeBoundary = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-before-boundary", cancellationToken);
        dbContext.Entry(epBeforeBoundary).Property(nameof(RuvEpisode.Created)).CurrentValue = cutoff - TimeSpan.FromTicks(1);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert: only the episode at the cutoff boundary is included; the episode one tick earlier is excluded
        result.RecentlyAddedEpisodes.Count.ShouldBe(1);
        result.RecentlyAddedEpisodes[0].ProgramName.ShouldBe("At Boundary Show");
    }

    [Fact]
    public async Task WhenElevenWindowedPrograms_ReturnsOnlyTen()
    {
        // Arrange: 11 programs each with one windowed episode; only 10 may appear
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        for (int i = 1; i <= 11; i++)
        {
            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(1700 + i)
                .WithName($"Cap Show {i}")
                .WithMultipleEpisodes()
                .Build();
            program.TryAddEpisode($"cap-ep-{i}", new Uri("http://test.com"), $"Cap Episode {i}", "Desc", new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
            dbContext.Set<RuvProgram>().Add(program);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (int i in Enumerable.Range(1, 11))
        {
            RuvEpisode ep = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == $"cap-ep-{i}", cancellationToken);
            dbContext.Entry(ep).Property(nameof(RuvEpisode.Created)).CurrentValue = now.UtcDateTime.AddDays(-4);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.RecentlyAddedEpisodes.Count.ShouldBe(10);
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
        failedItem.MarkFailed("test failure");

        dbContext.Set<DownloadQueueItem>().AddRange(downloadingItem, failedItem);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.Download.QueueDepth.ShouldBe(1);
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
        result.Download.ShouldNotBeNull();
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
        result.QueueStatus.Download.ShouldNotBeNull();
        result.QueueStatus.TvdbSeriesLookup.Depth.ShouldBeGreaterThanOrEqualTo(0);
        result.QueueStatus.TvdbEpisodeLookup.Depth.ShouldBeGreaterThanOrEqualTo(0);
        result.QueueStatus.Download.Depth.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ReturnsTvdbSeriesLookupCardInfo_WhenNoData()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.TvdbSeriesLookup.ShouldNotBeNull();
        result.TvdbSeriesLookup.IsProcessing.ShouldBeFalse();
        result.TvdbSeriesLookup.CurrentProgram.ShouldBeNull();
        result.TvdbSeriesLookup.PendingCount.ShouldBe(0);
        result.TvdbSeriesLookup.LastLookedUpAt.ShouldBeNull();
        result.TvdbSeriesLookup.RetryCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsTvdbSeriesLookupCardInfo_RetryCount_CountsProgramsWithNextLookup()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram retryingProgram = new RuvProgramBuilder()
            .WithRuvId(9001)
            .WithName("Retrying Show")
            .WithMultipleEpisodes()
            .Build();
        retryingProgram.ScheduleLookup();

        RuvProgram normalProgram = new RuvProgramBuilder()
            .WithRuvId(9002)
            .WithName("Normal Show")
            .WithMultipleEpisodes()
            .Build();

        dbContext.Set<RuvProgram>().AddRange(retryingProgram, normalProgram);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.TvdbSeriesLookup.RetryCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReturnsTvdbEpisodeLookupCardInfo_WhenNoData()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.TvdbEpisodeLookup.ShouldNotBeNull();
        result.TvdbEpisodeLookup.IsProcessing.ShouldBeFalse();
        result.TvdbEpisodeLookup.PendingCount.ShouldBeGreaterThanOrEqualTo(0);
        result.TvdbEpisodeLookup.LastLookedUpAt.ShouldBeNull();
        result.TvdbEpisodeLookup.RetryCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsTvdbEpisodeLookupCardInfo_RetryCount_CountsEpisodesWithNextLookup()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(10001)
            .WithName("Episode Retry Show")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder().WithName("Episode Retry Series").Build());
        program.TryAddEpisode("ep-retry", new Uri("http://test.com"), "Retrying Episode", "Desc", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep-normal", new Uri("http://test.com"), "Normal Episode", "Desc", new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(30));

        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode retryingEpisode = await dbContext.Set<RuvEpisode>().FirstAsync(e => e.RuvId == "ep-retry", cancellationToken);
        retryingEpisode.ScheduleLookup();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.TvdbEpisodeLookup.RetryCount.ShouldBe(1);
    }

    [Fact]
    public async Task ReturnsProgramRefreshCardInfo()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.ProgramRefresh.ShouldNotBeNull();
        result.ProgramRefresh.LastEnqueuedAt.ShouldBeNull();
        result.ProgramRefresh.LastEnqueuedCount.ShouldBeNull();
        result.ProgramRefresh.NextFireTimeUtc.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsEpisodeSyncCardInfo()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.EpisodeSync.ShouldSatisfyAllConditions(
            () => result.EpisodeSync.IsRunning.ShouldBeFalse(),
            () => result.EpisodeSync.Depth.ShouldBe(0),
            () => result.EpisodeSync.CompletedCount.ShouldBe(0),
            () => result.EpisodeSync.CurrentProgram.ShouldBeNull(),
            () => result.EpisodeSync.LastCompletedAt.ShouldBeNull(),
            () => result.EpisodeSync.StalledFor.ShouldBeNull(),
            () => result.EpisodeSync.NextFireTimeUtc.ShouldBeNull());
    }

    [Fact]
    public async Task WhenLastCompletedAtOlderThanTwoHours_IsStalled()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset lastCompleted = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset now = lastCompleted.AddHours(2).AddSeconds(1);
        FixedTimeProvider timeProvider = new(lastCompleted);
        ProgramRefreshNotifier notifier = new(timeProvider);
        notifier.Enqueue(1, "Program A");
        notifier.MarkComplete(1);

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        TimeSpan stalledFor = result.EpisodeSync.StalledFor.ShouldNotBeNull();
        stalledFor.ShouldBe(now - lastCompleted);
    }

    [Fact]
    public async Task WhenLastCompletedAtWithinTwoHours_IsNotStalled()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset lastCompleted = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);
        DateTimeOffset now = lastCompleted.AddHours(1);
        FixedTimeProvider timeProvider = new(lastCompleted);
        ProgramRefreshNotifier notifier = new(timeProvider);
        notifier.Enqueue(1, "Program A");
        notifier.MarkComplete(1);

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.EpisodeSync.StalledFor.ShouldBeNull();
    }

    [Fact]
    public async Task WhenNeverCompletedAndStartedAtOlderThanTwoHours_IsStalled()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset startedAt = new(2026, 9, 21, 6, 0, 0, TimeSpan.Zero);
        DateTimeOffset now = startedAt.AddHours(2).AddSeconds(1);
        ProgramRefreshNotifier notifier = new(new FixedTimeProvider(startedAt));

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        TimeSpan stalledForAc5 = result.EpisodeSync.StalledFor.ShouldNotBeNull();
        stalledForAc5.ShouldBe(now - startedAt);
    }

    [Fact]
    public async Task WhenNeverCompletedAndStartedAtWithinTwoHours_IsNotStalled()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset startedAt = new(2026, 9, 21, 8, 30, 0, TimeSpan.Zero);
        DateTimeOffset now = startedAt.AddHours(1);
        ProgramRefreshNotifier notifier = new(new FixedTimeProvider(startedAt));

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.EpisodeSync.StalledFor.ShouldBeNull();
    }

    [Fact]
    public async Task WhenElapsedEqualsThreshold_IsNotStalled()
    {
        // Arrange — elapsed is exactly 2× the refresh interval; "within" includes the boundary, so not stalled.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset lastCompleted = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
        TimeSpan threshold = TimeSpan.FromHours(RefreshSchedule.ProgramRefreshIntervalHours * 2);
        DateTimeOffset now = lastCompleted + threshold;
        FixedTimeProvider timeProvider = new(lastCompleted);
        ProgramRefreshNotifier notifier = new(timeProvider);
        notifier.Enqueue(1, "Program A");
        notifier.MarkComplete(1);

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        result.EpisodeSync.StalledFor.ShouldBeNull();
    }

    [Fact]
    public async Task WhenElapsedExceedsThresholdByOneSecond_IsStalled()
    {
        // Arrange — elapsed is exactly 2× the refresh interval plus one second; crosses the boundary.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        DateTimeOffset lastCompleted = new(2026, 9, 21, 8, 0, 0, TimeSpan.Zero);
        TimeSpan threshold = TimeSpan.FromHours(RefreshSchedule.ProgramRefreshIntervalHours * 2);
        DateTimeOffset now = lastCompleted + threshold + TimeSpan.FromSeconds(1);
        FixedTimeProvider timeProvider = new(lastCompleted);
        ProgramRefreshNotifier notifier = new(timeProvider);
        notifier.Enqueue(1, "Program A");
        notifier.MarkComplete(1);

        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider(now));
                services.RemoveAll<ProgramRefreshNotifier>();
                services.AddSingleton(notifier);
            }));

        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<GetDashboardQuery, DashboardData> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDashboardQuery, DashboardData>>();

        // Act
        DashboardData result = await handler.Handle(new GetDashboardQuery(), cancellationToken);

        // Assert
        TimeSpan stalledFor = result.EpisodeSync.StalledFor.ShouldNotBeNull();
        stalledFor.ShouldBe(now - lastCompleted);
    }

}
