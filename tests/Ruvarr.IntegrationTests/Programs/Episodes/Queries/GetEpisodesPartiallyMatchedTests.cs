using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Queries;

public sealed class GetEpisodesPartiallyMatchedTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsPartiallyMatchedPrograms_WhenFilterIsSet()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(10001, "RÚV1", "Partially Matched Show", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("1001", "Some Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("PM-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        program.TryAddEpisode("PM-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 5001, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes?isProgramPartiallyMatched=true", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(p => p.ProgramRuvId == 10001);
    }

    [Fact]
    public async Task ExcludesFullyMatchedPrograms_WhenPartiallyMatchedFilterIsSet()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(10002, "RÚV1", "Fully Matched Show", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("1002", "Fully Matched Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("FM-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        program.TryAddEpisode("FM-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 5002, season: 1, episode: 1, isMissing: false);
        program.Episodes[1].Match(tvdbId: 5003, season: 1, episode: 2, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes?isProgramPartiallyMatched=true", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain(p => p.ProgramRuvId == 10002);
    }

    [Fact]
    public async Task ExcludesFullyUnmatchedPrograms_WhenPartiallyMatchedFilterIsSet()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(10003, "RÚV1", "Fully Unmatched Show", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("1003", "Fully Unmatched Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("FU-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        program.TryAddEpisode("FU-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes?isProgramPartiallyMatched=true", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain(p => p.ProgramRuvId == 10003);
    }

    [Fact]
    public async Task ExcludesMoviePrograms_WhenPartiallyMatchedFilterIsSet()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(10004, "RÚV1", "Some Movie", null, multipleEpisodes: false);
        program.MatchTmdb(TmdbMovie.Create(9001, "Some Movie"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("MOV-EP1", new Uri("https://example.com/ep1.mp4"), "Part 1", "Desc", DateTime.UtcNow);
        program.TryAddEpisode("MOV-EP2", new Uri("https://example.com/ep2.mp4"), "Part 2", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 5010, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes?isProgramPartiallyMatched=true", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain(p => p.ProgramRuvId == 10004);
    }

    [Fact]
    public async Task ExcludesProgramsWithNoEpisodes_WhenPartiallyMatchedFilterIsSet()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(10005, "RÚV1", "No Episodes Show", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("1005", "No Episodes Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes?isProgramPartiallyMatched=true", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain(p => p.ProgramRuvId == 10005);
    }
}
