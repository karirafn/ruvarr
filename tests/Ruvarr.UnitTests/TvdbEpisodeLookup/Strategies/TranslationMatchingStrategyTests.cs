using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

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

        EpisodeMatchingContext context = new(program, seriesData, []);
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

        EpisodeMatchingContext context = new(program, seriesData, []);
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

        EpisodeMatchingContext context = new(program, seriesData, []);
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

        EpisodeMatchingContext context = new(program, seriesData, []);
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

        EpisodeMatchingContext context = new(program, seriesData, []);
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
    public async Task SetsIsMissingWhenTvdbIdIsInMissingSet()
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

        HashSet<int> missingTvdbIds = [101];
        EpisodeMatchingContext context = new(program, seriesData, missingTvdbIds);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].IsMissing.ShouldBeTrue();
    }

    [Fact]
    public async Task DoesNotOverwriteAlreadyMatchedEpisodeWhenTranslationNameCollides()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "MH-MR", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(200, 2026, 3, false);

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder()
            .WithId(101)
            .WithSeasonNumber(1990)
            .WithNumber(2)
            .WithNameTranslations("isl")
            .Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();
        _tvdb.GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("MH-MR", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, []);
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
        program.Episodes[0].Match(101, 1, 1, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(101).WithSeasonNumber(1).WithNumber(1).WithNameTranslations("isl").Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(102).WithSeasonNumber(1).WithNumber(2).WithNameTranslations("isl").Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();
        _tvdb.GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new EpisodeTranslation("Þáttur 2", "", "isl", true));

        EpisodeMatchingContext context = new(program, seriesData, []);
        TranslationMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        await _tvdb.DidNotReceive().GetEpisodeTranslationAsync(101, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _tvdb.Received(1).GetEpisodeTranslationAsync(102, Arg.Any<string>(), Arg.Any<CancellationToken>());
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(102);
    }
}
