using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Queries.GetPrograms;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramsHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task ReturnsProgramsWithoutEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20001, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("TP-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20001);
        found.ShouldNotBeNull();
        found.Channel.ShouldBe("RÚV1");
        found.ProgramName.ShouldBe("Test Program");
        found.SeriesName.ShouldBeNull();
        found.EpisodeCount.ShouldBe(1);
    }

    [Fact]
    public async Task FiltersByUnmatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram matchedSeries = RuvProgram.Create(20002, "RÚV1", "Matched Series Program", null, multipleEpisodes: true);
        matchedSeries.MatchTvdb(TvdbSeries.Create(9001, "Some Series"));
        dbContext.Set<RuvProgram>().Add(matchedSeries);

        RuvProgram unmatched = RuvProgram.Create(20003, "RÚV1", "Unmatched Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(unmatched);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, IsUnmatched: true, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20003);
        result.ShouldNotContain(p => p.ProgramRuvId == 20002);
    }

    [Fact]
    public async Task FiltersByMatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram matchedSeries = RuvProgram.Create(20050, "RÚV1", "Matched Series For Filter", null, multipleEpisodes: true);
        matchedSeries.MatchTvdb(TvdbSeries.Create(9050, "Some Series"));
        dbContext.Set<RuvProgram>().Add(matchedSeries);

        RuvProgram matchedMovie = RuvProgram.Create(20051, "RÚV1", "Matched Movie For Filter", null, multipleEpisodes: true);
        matchedMovie.MatchTmdb(TmdbMovie.Create(9051, "Some Movie"));
        dbContext.Set<RuvProgram>().Add(matchedMovie);

        RuvProgram unmatched = RuvProgram.Create(20052, "RÚV1", "Unmatched For Filter", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(unmatched);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, IsUnmatched: false, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20050);
        result.ShouldContain(p => p.ProgramRuvId == 20051);
        result.ShouldNotContain(p => p.ProgramRuvId == 20052);
    }

    [Fact]
    public async Task ExcludesSingleEpisodePrograms()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram multiEpisode = RuvProgram.Create(20010, "RÚV1", "Multi Episode Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(multiEpisode);

        RuvProgram singleEpisode = RuvProgram.Create(20011, "RÚV1", "Single Episode Program", null, multipleEpisodes: false);
        dbContext.Set<RuvProgram>().Add(singleEpisode);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20010);
        result.ShouldNotContain(p => p.ProgramRuvId == 20011);
    }

    [Fact]
    public async Task ExcludesSingleEpisodeProgramsWhenFilteringByOtherCriteria()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram multiEpisode = RuvProgram.Create(20020, "RÚV1", "Multi Episode Unmatched", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(multiEpisode);

        RuvProgram singleEpisode = RuvProgram.Create(20021, "RÚV1", "Single Episode Unmatched", null, multipleEpisodes: false);
        dbContext.Set<RuvProgram>().Add(singleEpisode);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, IsUnmatched: true, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20020);
        result.ShouldNotContain(p => p.ProgramRuvId == 20021);
    }

    [Fact]
    public async Task FiltersByNotMissingEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram missingProgram = RuvProgram.Create(20060, "RÚV1", "Program With Missing Episodes", null, multipleEpisodes: true);
        missingProgram.SetHasMissingEpisodes(true);
        dbContext.Set<RuvProgram>().Add(missingProgram);

        RuvProgram notMissingProgram = RuvProgram.Create(20061, "RÚV1", "Program Without Missing Episodes", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(notMissingProgram);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, IsMissing: false, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20061);
        result.ShouldNotContain(p => p.ProgramRuvId == 20060);
    }

    [Fact]
    public async Task FiltersByPendingLookup()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram pending = RuvProgram.Create(20030, "RÚV1", "Pending Lookup Program", null, multipleEpisodes: true);
        pending.ScheduleLookup();
        dbContext.Set<RuvProgram>().Add(pending);

        RuvProgram notPending = RuvProgram.Create(20031, "RÚV1", "No Pending Lookup Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(notPending);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, IsPendingLookup: true, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20030);
        result.ShouldNotContain(p => p.ProgramRuvId == 20031);
    }

    [Fact]
    public async Task FiltersByHasForeignName()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram withForeign = RuvProgram.Create(20032, "RÚV1", "Program With Foreign Name", "Foreign Name", multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(withForeign);

        RuvProgram withoutForeign = RuvProgram.Create(20033, "RÚV1", "Program Without Foreign Name", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(withoutForeign);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, HasForeignName: true, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20032);
        result.ShouldNotContain(p => p.ProgramRuvId == 20033);
    }

    [Fact]
    public async Task FiltersByNotPendingLookup()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram pending = RuvProgram.Create(20034, "RÚV1", "Pending Lookup Program 2", null, multipleEpisodes: true);
        pending.ScheduleLookup();
        dbContext.Set<RuvProgram>().Add(pending);

        RuvProgram notPending = RuvProgram.Create(20035, "RÚV1", "No Pending Lookup Program 2", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(notPending);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, IsPendingLookup: false, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20035);
        result.ShouldNotContain(p => p.ProgramRuvId == 20034);
    }

    [Fact]
    public async Task FiltersByNoForeignName()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram withForeign = RuvProgram.Create(20036, "RÚV1", "Program With Foreign Name 2", "Foreign Name", multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(withForeign);

        RuvProgram withoutForeign = RuvProgram.Create(20037, "RÚV1", "Program Without Foreign Name 2", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(withoutForeign);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, HasForeignName: false, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20037);
        result.ShouldNotContain(p => p.ProgramRuvId == 20036);
    }

    [Fact]
    public async Task ReturnsTvdbUrlWhenSeriesHasSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20022, "RÚV1", "Series With Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(3001, "Series With Slug", slug: "some-slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20022);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBe(new Uri("https://www.thetvdb.com/series/some-slug"));
    }

    [Fact]
    public async Task ReturnsTvdbUrlAsNullWhenSeriesHasNullSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20023, "RÚV1", "Series Without Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(3002, "Series Without Slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20023);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsTvdbUrlAsNullWhenProgramIsUnmatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20024, "RÚV1", "Unmatched Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20024);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsRuvUrlWhenProgramHasSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20040, "RÚV1", "Program With Slug", null, multipleEpisodes: true, slug: "test-slug");
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20040);
        found.ShouldNotBeNull();
        found.RuvUrl.ShouldBe(new Uri("https://www.ruv.is/sjonvarp/spila/test-slug/20040"));
    }

    [Fact]
    public async Task ReturnsRuvUrlAsNullWhenProgramHasNoSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(20041, "RÚV1", "Program Without Slug", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20041);
        found.ShouldNotBeNull();
        found.RuvUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsFullyMatchedWhenAllEpisodesHaveTvdbId()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30001, "RÚV1", "Fully Matched Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(6001, "Fully Matched Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("FM-EP1", new Uri("https://example.com/fm-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("FM-EP2", new Uri("https://example.com/fm-ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode ep1 = program.Episodes.First(e => e.RuvId == "FM-EP1");
        RuvEpisode ep2 = program.Episodes.First(e => e.RuvId == "FM-EP2");
        ep1.Match(tvdbId: 101, season: 1, episode: 1, isMissing: false);
        ep2.Match(tvdbId: 102, season: 1, episode: 2, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30001);
        found.ShouldNotBeNull();
        found.EpisodeMatchStatus.ShouldBe(EpisodeMatchStatus.FullyMatched);
    }

    [Fact]
    public async Task ReturnsPartiallyMatchedWhenSomeEpisodesHaveTvdbId()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30002, "RÚV1", "Partially Matched Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(6002, "Partially Matched Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("PM-EP1", new Uri("https://example.com/pm-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("PM-EP2", new Uri("https://example.com/pm-ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode ep1 = program.Episodes.First(e => e.RuvId == "PM-EP1");
        ep1.Match(tvdbId: 201, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30002);
        found.ShouldNotBeNull();
        found.EpisodeMatchStatus.ShouldBe(EpisodeMatchStatus.PartiallyMatched);
    }

    [Fact]
    public async Task ReturnsNoneMatchedWhenNoEpisodesHaveTvdbId()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30003, "RÚV1", "None Matched Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(6003, "None Matched Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("NM-EP1", new Uri("https://example.com/nm-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("NM-EP2", new Uri("https://example.com/nm-ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30003);
        found.ShouldNotBeNull();
        found.EpisodeMatchStatus.ShouldBe(EpisodeMatchStatus.NoneMatched);
    }

    [Fact]
    public async Task ReturnsNoneMatchedWhenProgramHasZeroEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30004, "RÚV1", "Zero Episodes Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(6004, "Zero Episodes Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30004);
        found.ShouldNotBeNull();
        found.EpisodeMatchStatus.ShouldBe(EpisodeMatchStatus.NoneMatched);
    }

    [Fact]
    public async Task ReturnsImageUrlAsNullToAvoidProjectingUnusedData()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30010, "RUV1", "Program With Image", null, multipleEpisodes: true,
            imageUrl: new Uri("https://example.com/hero.jpg"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30010);
        found.ShouldNotBeNull();
        found.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNoneMatchedWhenProgramHasNoSeries()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(30005, "RÚV1", "No Series Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("NS-EP1", new Uri("https://example.com/ns-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 30005);
        found.ShouldNotBeNull();
        found.EpisodeMatchStatus.ShouldBe(EpisodeMatchStatus.NoneMatched);
    }

    [Fact]
    public async Task ReturnsTvdbUrlAsNullWhenSlugIsMalformed()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        TvdbSeries series = TvdbSeries.Create(4001, "Series With Bad Slug");
        series.UpdateSlug("bad?slug=1");

        RuvProgram program = RuvProgram.Create(20025, "RÚV1", "Series With Bad Slug", null, multipleEpisodes: true);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20025);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }

    [Fact]
    public async Task FiltersByNotMonitored()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram monitored = RuvProgram.Create(20070, "RÚV1", "Monitored Program", null, multipleEpisodes: true);
        monitored.SetMonitoredStatus(true);
        dbContext.Set<RuvProgram>().Add(monitored);

        RuvProgram unmonitored = RuvProgram.Create(20071, "RÚV1", "Unmonitored Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(unmonitored);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, IsMonitored: false, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 20071);
        result.ShouldNotContain(p => p.ProgramRuvId == 20070);
    }

    [Fact]
    public async Task FiltersByEpisodeMatchFullyMatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram fullyMatched = RuvProgram.Create(40001, "RÚV1", "Fully Matched Filter", null, multipleEpisodes: true);
        fullyMatched.MatchTvdb(TvdbSeries.Create(7001, "FM Series"));
        dbContext.Set<RuvProgram>().Add(fullyMatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        fullyMatched.TryAddEpisode("FMF-EP1", new Uri("https://example.com/fmf-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);
        fullyMatched.Episodes.First(e => e.RuvId == "FMF-EP1").Match(tvdbId: 301, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvProgram noneMatched = RuvProgram.Create(40002, "RÚV1", "None Matched Filter", null, multipleEpisodes: true);
        noneMatched.MatchTvdb(TvdbSeries.Create(7002, "NM Series"));
        dbContext.Set<RuvProgram>().Add(noneMatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        noneMatched.TryAddEpisode("NMF-EP1", new Uri("https://example.com/nmf-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, EpisodeMatch: Contracts.EpisodeMatchStatus.FullyMatched), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 40001);
        result.ShouldNotContain(p => p.ProgramRuvId == 40002);
    }

    [Fact]
    public async Task FiltersByEpisodeMatchPartiallyMatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram partial = RuvProgram.Create(40003, "RÚV1", "Partial Match Filter", null, multipleEpisodes: true);
        partial.MatchTvdb(TvdbSeries.Create(7003, "PM Series"));
        dbContext.Set<RuvProgram>().Add(partial);
        await dbContext.SaveChangesAsync(cancellationToken);

        partial.TryAddEpisode("PMF-EP1", new Uri("https://example.com/pmf-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        partial.TryAddEpisode("PMF-EP2", new Uri("https://example.com/pmf-ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);
        partial.Episodes.First(e => e.RuvId == "PMF-EP1").Match(tvdbId: 401, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvProgram fullyMatched = RuvProgram.Create(40004, "RÚV1", "Fully Matched For Partial Filter", null, multipleEpisodes: true);
        fullyMatched.MatchTvdb(TvdbSeries.Create(7004, "FM Series 2"));
        dbContext.Set<RuvProgram>().Add(fullyMatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        fullyMatched.TryAddEpisode("FMF2-EP1", new Uri("https://example.com/fmf2-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);
        fullyMatched.Episodes.First(e => e.RuvId == "FMF2-EP1").Match(tvdbId: 402, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, EpisodeMatch: Contracts.EpisodeMatchStatus.PartiallyMatched), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 40003);
        result.ShouldNotContain(p => p.ProgramRuvId == 40004);
    }

    [Fact]
    public async Task FiltersByEpisodeMatchNoneMatched()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram noneMatched = RuvProgram.Create(40005, "RÚV1", "None Matched For Filter", null, multipleEpisodes: true);
        noneMatched.MatchTvdb(TvdbSeries.Create(7005, "NM Series 2"));
        dbContext.Set<RuvProgram>().Add(noneMatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        noneMatched.TryAddEpisode("NMF2-EP1", new Uri("https://example.com/nmf2-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvProgram partial = RuvProgram.Create(40006, "RÚV1", "Partial For None Filter", null, multipleEpisodes: true);
        partial.MatchTvdb(TvdbSeries.Create(7006, "PM Series 2"));
        dbContext.Set<RuvProgram>().Add(partial);
        await dbContext.SaveChangesAsync(cancellationToken);

        partial.TryAddEpisode("PMF2-EP1", new Uri("https://example.com/pmf2-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);
        partial.Episodes.First(e => e.RuvId == "PMF2-EP1").Match(tvdbId: 501, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, EpisodeMatch: Contracts.EpisodeMatchStatus.NoneMatched), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        result.ShouldContain(p => p.ProgramRuvId == 40005);
        result.ShouldNotContain(p => p.ProgramRuvId == 40006);
    }

    [Fact]
    public async Task ReturnsEpisodeCount()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(50001, "RÚV1", "Episode Count Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EC-EP1", new Uri("https://example.com/ec-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("EC-EP2", new Uri("https://example.com/ec-ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("EC-EP3", new Uri("https://example.com/ec-ep3.mp4"), "Episode 3", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 50001);
        found.ShouldNotBeNull();
        found.EpisodeCount.ShouldBe(3);
    }

    [Fact]
    public async Task ReturnsZeroEpisodeCountWhenProgramHasNoEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler =
            scope.ServiceProvider.GetRequiredService<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();

        RuvProgram program = RuvProgram.Create(50002, "RÚV1", "Zero Episode Count Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary> result = await handler
            .Handle(new GetProgramsQuery(null, null, null, null, null, null, null), cancellationToken)
            .ToListAsync(cancellationToken);

        // Assert
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 50002);
        found.ShouldNotBeNull();
        found.EpisodeCount.ShouldBe(0);
    }
}
