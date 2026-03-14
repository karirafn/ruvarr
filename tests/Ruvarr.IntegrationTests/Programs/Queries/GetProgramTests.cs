using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsOkWithCorrectShape()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40001)
            .WithChannel("RÚV1")
            .WithName("Detail Test Program")
            .WithMultipleEpisodes()
            .Build();
        program.MatchTvdb(new TvdbSeriesBuilder()
            .WithName("Detail Series")
            .WithSlug("detail-series")
            .Build());
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await factory.CreateClient()
            .GetFromJsonAsync<ProgramSummary>("/programs/40001", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ProgramRuvId.ShouldBe(40001);
        result.ProgramName.ShouldBe("Detail Test Program");
        result.Channel.ShouldBe("RÚV1");
        result.SeriesName.ShouldBe("Detail Series");
        result.TvdbUrl.ShouldBe(new Uri("https://www.thetvdb.com/series/detail-series"));
    }

    [Fact]
    public async Task ReturnsNotFoundForUnknownRuvId()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        HttpResponseMessage response = await factory.CreateClient()
            .GetAsync("/programs/99999", cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
