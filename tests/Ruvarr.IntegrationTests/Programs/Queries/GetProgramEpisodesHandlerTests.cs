using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Queries.GetProgramEpisodes;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramEpisodesHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task ReturnsEpisodesInOrder()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30002, "RÚV1", "Ordered Episodes Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(8001, "Ordered Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("OE-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("OE-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 7002, season: 1, episode: 2, isMissing: false);
        program.Episodes[1].Match(tvdbId: 7001, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30002), cancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result[0].TvdbMatches[0].EpisodeNumber.ShouldBe(1);
        result[1].TvdbMatches[0].EpisodeNumber.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsEmptyList()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30003, "RÚV1", "No Episodes Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30003), cancellationToken);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsRuvUrlWhenSlugPresent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30004, "RÚV1", "Slug Program", null, multipleEpisodes: true, slug: "frettir");
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("ab1234", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30004), cancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].RuvUrl.ShouldBe(new Uri("https://www.ruv.is/sjonvarp/spila/frettir/30004/ab1234"));
    }

    [Fact]
    public async Task ReturnsDuration()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30006, "RÚV1", "Duration Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("DUR-EP1", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("DUR-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.Zero);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30006), cancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        result.Single(x => x.EpisodeRuvId == "DUR-EP1").Duration.ShouldBe(TimeSpan.FromMinutes(30));
        result.Single(x => x.EpisodeRuvId == "DUR-EP2").Duration.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task ReturnsTvdbUrlForMatchedEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30007, "RÚV1", "TVDB URL Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(9001, "TVDB URL Series", slug: "tvdb-url-series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("TVDB-EP1", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("TVDB-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 5001, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30007), cancellationToken);

        // Assert
        result.Count.ShouldBe(2);
        EpisodeSummary matched = result.Single(x => x.EpisodeRuvId == "TVDB-EP1");
        matched.TvdbMatches[0].TvdbUrl.ShouldBe(new Uri("https://www.thetvdb.com/series/tvdb-url-series/episodes/5001"));

        EpisodeSummary unmatched = result.Single(x => x.EpisodeRuvId == "TVDB-EP2");
        unmatched.TvdbMatches.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsNullRuvUrlWhenSlugAbsent()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();

        RuvProgram program = RuvProgram.Create(30005, "RÚV1", "No Slug Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("cd5678", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary> result = await handler.Handle(new GetProgramEpisodesQuery(30005), cancellationToken);

        // Assert
        result.Count.ShouldBe(1);
        result[0].RuvUrl.ShouldBeNull();
    }
}
