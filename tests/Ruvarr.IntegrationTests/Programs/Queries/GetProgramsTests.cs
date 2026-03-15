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
        matched.MatchTvdb(TvdbSeries.Create(9001, "Some Series"));
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

    [Fact]
    public async Task ExcludesSingleEpisodePrograms()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram multiEpisode = RuvProgram.Create(20010, "RÚV1", "Multi Episode Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(multiEpisode);

        RuvProgram singleEpisode = RuvProgram.Create(20011, "RÚV1", "Single Episode Program", null, multipleEpisodes: false);
        dbContext.Set<RuvProgram>().Add(singleEpisode);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
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

        RuvProgram multiEpisode = RuvProgram.Create(20020, "RÚV1", "Multi Episode Unmatched", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(multiEpisode);

        RuvProgram singleEpisode = RuvProgram.Create(20021, "RÚV1", "Single Episode Unmatched", null, multipleEpisodes: false);
        dbContext.Set<RuvProgram>().Add(singleEpisode);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs?isProgramMatched=false", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(p => p.ProgramRuvId == 20020);
        result.ShouldNotContain(p => p.ProgramRuvId == 20021);
    }

    [Fact]
    public async Task FiltersByUnmatchedEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram withUnmatched = RuvProgram.Create(20030, "RÚV1", "Program With Unmatched Episodes", null, multipleEpisodes: true);
        withUnmatched.MatchTvdb(TvdbSeries.Create(5001, "Some Series"));
        dbContext.Set<RuvProgram>().Add(withUnmatched);
        await dbContext.SaveChangesAsync(cancellationToken);
        withUnmatched.TryAddEpisode("WU-EP1", new Uri("https://example.com/wu-ep1.mp4"), "Episode 1", "Desc", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvProgram allMatched = RuvProgram.Create(20031, "RÚV1", "Program With All Matched Episodes", null, multipleEpisodes: true);
        allMatched.MatchTvdb(TvdbSeries.Create(5002, "Other Series"));
        dbContext.Set<RuvProgram>().Add(allMatched);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs?isEpisodeMatched=false", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain(p => p.ProgramRuvId == 20030);
        result.ShouldNotContain(p => p.ProgramRuvId == 20031);
    }

    [Fact]
    public async Task ReturnsTvdbUrlWhenSeriesHasSlug()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(20022, "RÚV1", "Series With Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(3001, "Series With Slug", slug: "some-slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
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

        RuvProgram program = RuvProgram.Create(20023, "RÚV1", "Series Without Slug", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(3002, "Series Without Slug"));
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
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

        RuvProgram program = RuvProgram.Create(20024, "RÚV1", "Unmatched Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20024);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsTvdbUrlAsNullWhenSlugIsMalformed()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        TvdbSeries series = TvdbSeries.Create(4001, "Series With Bad Slug");
        series.UpdateSlug("bad?slug=1");

        RuvProgram program = RuvProgram.Create(20025, "RÚV1", "Series With Bad Slug", null, multipleEpisodes: true);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        List<ProgramSummary>? result = await factory.CreateClient()
            .GetFromJsonAsync<List<ProgramSummary>>("/programs", cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        ProgramSummary? found = result.FirstOrDefault(p => p.ProgramRuvId == 20025);
        found.ShouldNotBeNull();
        found.TvdbUrl.ShouldBeNull();
    }
}
