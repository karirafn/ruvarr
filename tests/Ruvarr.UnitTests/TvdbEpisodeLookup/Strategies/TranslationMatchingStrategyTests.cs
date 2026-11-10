using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Strategies;

using Shouldly;

namespace Ruvarr.UnitTests.TvdbEpisodeLookup.Strategies;

public sealed class TranslationMatchingStrategyTests
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();

    private TranslationMatchingStrategy CreateSut() => new(
        _tvdb,
        NullLogger<TranslationMatchingStrategy>.Instance);

    [Fact]
    public async Task MatchesEpisodeWhenTranslationMatchesTitle()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1)
            .WithNumber(1)
            .WithNameTranslations("isl")
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(101);
    }

    [Fact]
    public async Task SkipsEpisodeWhenTranslationNotFound()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((EpisodeTranslation?)null);

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsEpisodeWhenNoIslTranslation()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("eng").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        _ = _tvdb.DidNotReceive().GetEpisodeTranslationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsEpisodeWhenTitleDoesNotMatch()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(101).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Different title", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task FetchesAllTranslationsForMultipleEpisodes()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0003", new Uri("http://test.com"), "Þáttur 3", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(102).WithSeasonNumber(1).WithNumber(2).WithNameTranslations("isl").Build();
        Episode tvdbEpisode3 = new TvdbEpisodeDataBuilder()
            .WithId(103).WithSeasonNumber(1).WithNumber(3).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2, tvdbEpisode3).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));
        _tvdb.GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 2", "", "isl", true));
        _tvdb.GetEpisodeTranslationAsync(103, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 3", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(101);
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(102);
        program.Episodes[2].TvdbEpisodes[0].TvdbId.ShouldBe(103);
        await _tvdb.Received(3).GetEpisodeTranslationAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetsSonarrStatusFromLookup()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 1", "", "isl", true));

        SonarrEpisodeStatuses sonarrStatuses = SonarrStatuses.Missing(101);
        EpisodeMatchingContext context = new(program, seriesData, sonarrStatuses);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.Missing);
    }

    [Fact]
    public async Task DoesNotOverwriteAlreadyMatchedEpisodeWhenTranslationNameCollides()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "MH-MR", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(200, 2026, 3, SonarrStatuses.NotTracked, MatchOrigin.Automatic);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1990)
            .WithNumber(2)
            .WithNameTranslations("isl")
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("MH-MR", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.Count.ShouldBe(1);
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(200);
        program.Episodes[0].TvdbEpisodes[0].SeasonNumber.ShouldBe(2026);
        program.Episodes[0].TvdbEpisodes[0].EpisodeNumber.ShouldBe(3);
    }

    [Fact]
    public async Task SkipsTranslationLookupForAlreadyMatchedEpisodes()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(101, 1, 1, SonarrStatuses.NotTracked, MatchOrigin.Automatic);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(102).WithSeasonNumber(1).WithNumber(2).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();
        _tvdb.GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 2", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        await _tvdb.DidNotReceive().GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _tvdb.Received(1).GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>());
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(102);
    }

    private static RuvProgram ProgramWithEpisode(string programName, string title)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName(programName).WithMultipleEpisodes().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), title, "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        return program;
    }

    private Episode TranslatedEpisode(int id, int season, int number, string islName)
    {
        _tvdb.GetEpisodeTranslationAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation(islName, "", "isl", true));
        return new TvdbEpisodeDataBuilder()
            .WithId(id).WithSeasonNumber(season).WithNumber(number).WithNameTranslations("isl").Build();
    }

    private async Task MatchAsync(RuvProgram program, params Episode[] tvdbEpisodes)
    {
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisodes).Build();
        await CreateSut().MatchAsync(new EpisodeMatchingContext(program, seriesData, SonarrStatuses.NotTracked), CancellationToken.None);
    }

    [Fact]
    public async Task YearProgram_DoesNotMatchTheSameTitleFromAnotherYear()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Gettu betur 1999", "MR - Versló");

        // Act
        await MatchAsync(
            program,
            TranslatedEpisode(1998_01, 1998, 1, "Annað"),
            TranslatedEpisode(2024_05, 2024, 5, "MR - Versló"));

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task YearProgram_MatchesTheTitleInItsOwnYear()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Gettu betur 1998", "MR - Versló");

        // Act
        await MatchAsync(
            program,
            TranslatedEpisode(1998_03, 1998, 3, "MR - Versló"),
            TranslatedEpisode(2024_05, 2024, 5, "MR - Versló"));

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(1998_03);
    }

    [Fact]
    public async Task PrefersTheDeclaredSeasonOverAnEarlierSeasonWithTheSameTitle()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Í garðinum með Gurrý IV", "Þáttur 1 af 6");

        // Act
        await MatchAsync(
            program,
            TranslatedEpisode(301, 3, 1, "Þáttur 1 af 6"),
            TranslatedEpisode(401, 4, 1, "Þáttur 1 af 6"));

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(401);
    }

    [Fact]
    public async Task FallsBackToTheOnlySeasonCarryingTheTitle()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Andri og Edda I", "Á ferð");

        // Act
        await MatchAsync(
            program,
            TranslatedEpisode(101, 1, 1, "Annað"),
            TranslatedEpisode(305, 3, 5, "Á ferð"));

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(305);
    }

    [Fact]
    public async Task DoesNotMatchWhenTheTitleRecursAcrossOtherSeasons()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Gettu betur", "MH - MR");

        // Act
        await MatchAsync(
            program,
            TranslatedEpisode(1990_02, 1990, 2, "MH - MR"),
            TranslatedEpisode(2026_03, 2026, 3, "MH - MR"));

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordsAutomaticOrigin()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Show", "Þáttur 1");

        // Act
        await MatchAsync(program, TranslatedEpisode(101, 1, 1, "Þáttur 1"));

        // Assert
        program.Episodes[0].MatchOrigin.ShouldBe(MatchOrigin.Automatic);
    }

    [Fact]
    public async Task DoesNotLinkOneTvdbEpisodeToTwoRuvEpisodes()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Show", "Þáttur 1");
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        // Act
        await MatchAsync(program, TranslatedEpisode(101, 1, 1, "Þáttur 1"));

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Count == 0);
    }

    [Fact]
    public async Task ResultDoesNotDependOnTheOrderTranslationsArrive()
    {
        // Arrange
        RuvProgram program = ProgramWithEpisode("Í garðinum með Gurrý IV", "Þáttur 1 af 6");
        Episode inDeclaredSeason = TranslatedEpisode(401, 4, 1, "Þáttur 1 af 6");
        Episode inEarlierSeason = new TvdbEpisodeDataBuilder()
            .WithId(301).WithSeasonNumber(3).WithNumber(1).WithNameTranslations("isl").Build();
        // The earlier season's translation arrives last, after the declared season's one.
        _tvdb.GetEpisodeTranslationAsync(301, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                await Task.Delay(50);
                return (EpisodeTranslation?)new EpisodeTranslation("Þáttur 1 af 6", "", "isl", true);
            });

        // Act
        await MatchAsync(program, inEarlierSeason, inDeclaredSeason);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldHaveSingleItem().TvdbId.ShouldBe(401);
    }
}
