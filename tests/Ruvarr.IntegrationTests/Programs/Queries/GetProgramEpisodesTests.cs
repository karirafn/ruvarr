using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramEpisodesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsOk()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(30001, "RÚV1", "Episode Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        HttpResponseMessage result = await factory.CreateClient().GetAsync("/programs/30001/episodes", cancellationToken);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsEpisodesInOrder()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(30002, "RÚV1", "Ordered Episodes Program", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("8001", "Ordered Series"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("OE-EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow);
        program.TryAddEpisode("OE-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.Episodes[0].Match(tvdbId: 7002, season: 1, episode: 2, isMissing: false);
        program.Episodes[1].Match(tvdbId: 7001, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<EpisodeSummary>>("/programs/30002/episodes", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].EpisodeNumber.ShouldBe(1);
        result[1].EpisodeNumber.ShouldBe(2);
    }

    [Fact]
    public async Task ReturnsEmptyList()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(30003, "RÚV1", "No Episodes Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<EpisodeSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<EpisodeSummary>>("/programs/30003/episodes", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }
}
