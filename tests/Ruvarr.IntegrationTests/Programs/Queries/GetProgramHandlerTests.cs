using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Queries.GetProgram;

using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Queries;

public sealed class GetProgramHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task ReturnsCorrectShape()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

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
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40001), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ProgramRuvId.ShouldBe(40001);
        result.ProgramName.ShouldBe("Detail Test Program");
        result.Channel.ShouldBe("RÚV1");
        result.SeriesName.ShouldBe("Detail Series");
        result.TvdbUrl.ShouldBe(new Uri("https://www.thetvdb.com/series/detail-series"));
    }

    [Fact]
    public async Task ReturnsNullForUnknownRuvId()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(99999), cancellationToken);

        // Assert
        result.ShouldBeNull();
    }
}
