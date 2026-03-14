using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsOk()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage result = await factory.CreateClient().GetAsync("/programs", cancellationToken);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReturnsProgramsWithoutEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(20001, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("TP-EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20001);
        found.ShouldNotBeNull();
        found.Channel.ShouldBe("RÚV1");
        found.ProgramName.ShouldBe("Test Program");
        found.SeriesName.ShouldBeNull();
    }

    [Fact]
    public async Task FiltersByUnmatchedPrograms()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram matched = RuvProgram.Create(20002, "RÚV1", "Matched Program", null, multipleEpisodes: true);
        matched.MatchTvdb(TvdbSeries.Create("9001", "Some Series"));
        dbContext.Set<RuvProgram>().Add(matched);

        RuvProgram unmatched = RuvProgram.Create(20003, "RÚV1", "Unmatched Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(unmatched);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs?isProgramMatched=false", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(p => p.ProgramRuvId == 20003);
        result.ShouldNotContain(p => p.ProgramRuvId == 20002);
    }
}
