
using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Queries;

public sealed class GetEpisodesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsOk()
    {
        // Arrange

        // Act
        HttpResponseMessage result = await factory.CreateClient().GetAsync("/programs/episodes", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsTvdbUrl_WhenSeriesHasSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(40001, "RÚV1", "Series With Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("2001", "Series With Slug", slug: "series-with-slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("SWS-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramDetails>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramDetails>>("/programs/episodes", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramDetails? found = result.FirstOrDefault(p => p.ProgramRuvId == 40001);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBe(new Uri("https://www.thetvdb.com/series/series-with-slug"));
    }

    [Fact]
    public async Task ReturnsTvdbUrlNull_WhenSeriesHasNoSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(40002, "RÚV1", "Series Without Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create("2002", "Series Without Slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("SNS-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramDetails>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramDetails>>("/programs/episodes", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramDetails? found = result.FirstOrDefault(p => p.ProgramRuvId == 40002);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsTvdbUrlNull_WhenProgramHasNoSeries()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(40003, "RÚV1", "Unmatched Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("UP-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramDetails>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramDetails>>("/programs/episodes", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramDetails? found = result.FirstOrDefault(p => p.ProgramRuvId == 40003);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }
}
