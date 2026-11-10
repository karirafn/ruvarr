using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Queries.GetEpisodeMatchSuggestion;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Queries.GetEpisodeMatchSuggestion;

public sealed class GetEpisodeMatchSuggestionHandlerTests
{
    private const string RuvEpisodeId = "ep0001";
    private const string SiblingRuvEpisodeId = "ep0002";
    private const int TvdbSeriesId = 1000;

    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public GetEpisodeMatchSuggestionHandlerTests()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private async Task<RuvProgram> SeedAsync(string programName, string episodeTitle, Action<RuvProgram>? arrange = null)
    {
        using RuvarrDbContext seedContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName(programName).WithForeignName(null).Build();
        program.TryAddEpisode(RuvEpisodeId, new Uri("http://test.com"), episodeTitle, "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(TvdbSeriesId).Build());
        arrange?.Invoke(program);
        seedContext.Set<RuvProgram>().Add(program);
        await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return program;
    }

    private static void AddLinkedSibling(RuvProgram program, int tvdbId, int season)
    {
        program.TryAddEpisode(SiblingRuvEpisodeId, new Uri("http://test.com"), "Sibling", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.Episodes.Single(x => x.RuvId == SiblingRuvEpisodeId)
            .Match(tvdbId, season, episode: 1, SonarrStatuses.NotTracked, MatchOrigin.Automatic);
    }

    private void GivenSeries(params Episode[] episodes) =>
        _tvdb.GetSeriesAsync(TvdbSeriesId, Arg.Any<CancellationToken>())
            .Returns(new TvdbSeriesDataBuilder().WithId(TvdbSeriesId).WithEpisodes(episodes).Build());

    private void GivenIslTitle(int tvdbId, string name) =>
        _tvdb.GetEpisodeTranslationAsync(tvdbId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation(name, "", "isl", true));

    private static Episode TvdbEpisode(int id, int season, int number, bool hasIslTranslation = false) =>
        new TvdbEpisodeDataBuilder()
            .WithId(id)
            .WithSeasonNumber(season)
            .WithNumber(number)
            .WithNameTranslations(hasIslTranslation ? ["isl"] : ["eng"])
            .Build();

    private async Task<EpisodeMatchSuggestion?> HandleAsync(string ruvId = RuvEpisodeId)
    {
        using RuvarrDbContext dbContext = CreateDbContext();
        GetEpisodeMatchSuggestionHandler sut = new(dbContext, _tvdb);
        return await sut.Handle(new GetEpisodeMatchSuggestionQuery(ruvId), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhenTitleMatchesOneEpisodeOutsideTheDeclaredSeason_SuggestsIt()
    {
        // Arrange
        await SeedAsync("Gettu betur 1999", "Heimkoman");
        GivenSeries(
            TvdbEpisode(101, 1999, 1, hasIslTranslation: true),
            TvdbEpisode(201, 2000, 4, hasIslTranslation: true));
        GivenIslTitle(101, "Annar þáttur");
        GivenIslTitle(201, "Heimkoman");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBe(new SuggestedEpisode(201, MatchSuggestionReason.OutsideDeclaredSeason));
    }

    [Fact]
    public async Task WhenTitleMatchesSeveralEpisodesOutsideTheDeclaredSeason_SuggestsNothing()
    {
        // Arrange
        await SeedAsync("Gettu betur 1999", "Heimkoman");
        GivenSeries(
            TvdbEpisode(101, 1998, 1, hasIslTranslation: true),
            TvdbEpisode(201, 2000, 4, hasIslTranslation: true));
        GivenIslTitle(101, "Heimkoman");
        GivenIslTitle(201, "Heimkoman");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheOutsideEpisodeIsLinkedToAnotherEpisode_SuggestsNothing()
    {
        // Arrange
        await SeedAsync("Gettu betur 1999", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 201, season: 2000));
        GivenSeries(TvdbEpisode(201, 2000, 4, hasIslTranslation: true));
        GivenIslTitle(201, "Heimkoman");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task WhenNoSeasonIsDeclared_FetchesNoTranslations()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman");
        GivenSeries(TvdbEpisode(201, 2, 4, hasIslTranslation: true));
        GivenIslTitle(201, "Heimkoman");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBeNull();
        await _tvdb.DidNotReceiveWithAnyArgs().GetEpisodeTranslationAsync(default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WhenTitleIsAnEpisodeNumber_SuggestsThatEpisodeOfTheDefaultSeason()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4");
        GivenSeries(
            TvdbEpisode(104, 1, 4),
            TvdbEpisode(204, 2, 4));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBe(new SuggestedEpisode(204, MatchSuggestionReason.EpisodeNumber));
    }

    [Fact]
    public async Task WhenTheOutsideMatchIsAmbiguous_FallsBackToTheEpisodeNumber()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4");
        GivenSeries(
            TvdbEpisode(104, 1, 4, hasIslTranslation: true),
            TvdbEpisode(204, 2, 4),
            TvdbEpisode(304, 3, 4, hasIslTranslation: true));
        GivenIslTitle(104, "Þáttur 4");
        GivenIslTitle(304, "Þáttur 4");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBe(new SuggestedEpisode(204, MatchSuggestionReason.EpisodeNumber));
    }

    [Fact]
    public async Task WhenBothRulesApply_TheOutsideMatchWins()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4");
        GivenSeries(
            TvdbEpisode(204, 2, 4),
            TvdbEpisode(304, 3, 4, hasIslTranslation: true));
        GivenIslTitle(304, "Þáttur 4");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBe(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason));
    }

    [Fact]
    public async Task WhenTheDefaultSeasonHasNoSuchEpisode_SuggestsNothing()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 9");
        GivenSeries(TvdbEpisode(204, 2, 4), TvdbEpisode(109, 1, 9));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheEpisodeIsAlreadyMatched_SuggestsNothing()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4", p =>
            p.Episodes.Single(x => x.RuvId == RuvEpisodeId)
                .Match(104, season: 1, episode: 4, SonarrStatuses.NotTracked, MatchOrigin.Manual));
        GivenSeries(TvdbEpisode(104, 1, 4), TvdbEpisode(204, 2, 4));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().Suggestion.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheEpisodeIsUnknown_ReturnsNull()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4");
        GivenSeries(TvdbEpisode(204, 2, 4));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync("unknown");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheSeriesCannotBeLoaded_ReturnsNull()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 2", "Þáttur 4");
        _tvdb.GetSeriesAsync(TvdbSeriesId, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsTheDeclaredSeasons()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 5", "Heimkoman");
        GivenSeries(TvdbEpisode(501, 5, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DeclaredSeasons.ShouldBe([5]);
    }

    [Fact]
    public async Task WithoutADeclaredSeason_ReturnsNoDeclaredSeasons()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman");
        GivenSeries(TvdbEpisode(501, 5, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DeclaredSeasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task DefaultSeason_IsTheDeclaredSeasonBeforeSiblingLinks()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 3", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 201, season: 2));
        GivenSeries(TvdbEpisode(101, 1, 1), TvdbEpisode(201, 2, 1), TvdbEpisode(301, 3, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(3);
    }

    [Fact]
    public async Task DefaultSeason_WhenTheDeclaredSeasonIsNotOnTvdb_IsTheLowestSiblingSeason()
    {
        // Arrange
        await SeedAsync("Heimildarþættir 7", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 201, season: 2));
        GivenSeries(TvdbEpisode(101, 1, 1), TvdbEpisode(201, 2, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultSeason_WithoutADeclaredSeason_IsTheLowestSiblingSeason()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 201, season: 2));
        GivenSeries(TvdbEpisode(101, 1, 1), TvdbEpisode(201, 2, 1), TvdbEpisode(301, 3, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultSeason_IgnoresSiblingLinksToSpecials()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 1, season: 0));
        GivenSeries(TvdbEpisode(1, 0, 1), TvdbEpisode(201, 2, 1), TvdbEpisode(301, 3, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultSeason_WhenTheSiblingSeasonIsNotOnTvdb_IsTheEarliestSeason()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman", p => AddLinkedSibling(p, tvdbId: 501, season: 5));
        GivenSeries(TvdbEpisode(201, 2, 1), TvdbEpisode(301, 3, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultSeason_WithoutSiblingLinks_IsTheEarliestSeason()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman");
        GivenSeries(TvdbEpisode(1, 0, 1), TvdbEpisode(301, 3, 1), TvdbEpisode(201, 2, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBe(2);
    }

    [Fact]
    public async Task DefaultSeason_WithoutRegularSeasons_IsNull()
    {
        // Arrange
        await SeedAsync("Heimildarþættir", "Heimkoman");
        GivenSeries(TvdbEpisode(1, 0, 1));

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull().DefaultSeason.ShouldBeNull();
    }

    [Fact]
    public async Task WithManualSeasons_SuggestsTheTitleMatchOutsideThemAndReportsThemAsDeclared()
    {
        // Arrange
        await SeedAsync("Heimkoman", "Heimkoman", p => p.SetManualSeasons([2, 3], [1, 2, 3]));
        GivenSeries(
            TvdbEpisode(101, 1, 4, hasIslTranslation: true),
            TvdbEpisode(201, 2, 1),
            TvdbEpisode(301, 3, 1));
        GivenIslTitle(101, "Heimkoman");

        // Act
        EpisodeMatchSuggestion? result = await HandleAsync();

        // Assert
        result.ShouldNotBeNull();
        result.Suggestion.ShouldBe(new SuggestedEpisode(101, MatchSuggestionReason.OutsideDeclaredSeason));
        result.DeclaredSeasons.ShouldBe([2, 3]);
        result.DefaultSeason.ShouldBe(2);
    }
}
