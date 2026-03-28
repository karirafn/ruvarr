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
        result.EpisodeCount.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsRuvUrlWhenProgramHasSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40010)
            .WithSlug("detail-slug")
            .WithMultipleEpisodes()
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40010), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.RuvUrl.ShouldBe(new Uri("https://www.ruv.is/sjonvarp/spila/detail-slug/40010"));
    }

    [Fact]
    public async Task ReturnsRuvUrlAsNullWhenProgramHasNoSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40011)
            .WithMultipleEpisodes()
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40011), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.RuvUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsImageUrlWhenProgramHasImageUrl()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40020)
            .WithMultipleEpisodes()
            .WithImageUrl(new Uri("https://example.com/hero.jpg"))
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40020), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ImageUrl.ShouldBe(new Uri("https://example.com/hero.jpg"));
    }

    [Fact]
    public async Task ReturnsImageUrlAsNullWhenProgramHasNoImageUrl()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40021)
            .WithMultipleEpisodes()
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40021), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsDescriptionWhenProgramHasDescription()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40030)
            .WithMultipleEpisodes()
            .WithDescription("First paragraph.\n\nSecond paragraph.")
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40030), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Description.ShouldBe("First paragraph.\n\nSecond paragraph.");
    }

    [Fact]
    public async Task ReturnsDescriptionAsNullWhenProgramHasNoDescription()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40031)
            .WithMultipleEpisodes()
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40031), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Description.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsEpisodeCount()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<GetProgramQuery, ProgramSummary?> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(40040)
            .WithMultipleEpisodes()
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP1", new Uri("https://example.com/ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("EP2", new Uri("https://example.com/ep2.mp4"), "Episode 2", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        ProgramSummary? result = await handler.Handle(new GetProgramQuery(40040), cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.EpisodeCount.ShouldBe(2);
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
