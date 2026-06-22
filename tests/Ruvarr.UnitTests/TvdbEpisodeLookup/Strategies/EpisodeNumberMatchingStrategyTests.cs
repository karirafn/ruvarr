using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Strategies;

using Shouldly;

namespace Ruvarr.UnitTests.TvdbEpisodeLookup.Strategies;

public sealed class EpisodeNumberMatchingStrategyTests
{
    private static EpisodeNumberMatchingStrategy CreateSut() => new(
        NullLogger<EpisodeNumberMatchingStrategy>.Instance);

    [Fact]
    public async Task MatchesEpisodeWhenSeasonAndCountMatch()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(201);
        program.Episodes[0].TvdbEpisodes[0].SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public async Task SkipsWhenSeasonIsZero()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsWhenEpisodeCountMismatch()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsWhenEpisodeTitleCannotBeParsed()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Mynd", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task OnlyProcessesUnmatchedEpisodes()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(201, 2, 1, false);

        Episode tvdbEpisode201 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode202 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode201, tvdbEpisode202).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(201);
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(202);
    }

    [Fact]
    public async Task IntegerSeasonSuffixUsed()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show 3").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode = new TvdbEpisodeDataBuilder().WithId(301).WithSeasonNumber(3).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes(tvdbEpisode).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(301);
        program.Episodes[0].TvdbEpisodes[0].SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public async Task MatchesWhenOtherSeasonSharesEpisodeCount()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode4 = new TvdbEpisodeDataBuilder().WithId(401).WithSeasonNumber(4).WithNumber(1).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode2, tvdbEpisode4).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(201);
        program.Episodes[0].TvdbEpisodes[0].SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public async Task WhenSomeEpisodesAlreadyMatched_RemainingEpisodesMatchByNumber()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show II").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0003", new Uri("http://test.com"), "þáttur 3", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0004", new Uri("http://test.com"), "þáttur 4", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0005", new Uri("http://test.com"), "þáttur 5", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0006", new Uri("http://test.com"), "þáttur 6", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(201, 2, 1, false);
        program.Episodes[1].Match(202, 2, 2, false);
        program.Episodes[2].Match(203, 2, 3, false);
        program.Episodes[3].Match(204, 2, 4, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        Episode tvdbEpisode3 = new TvdbEpisodeDataBuilder().WithId(203).WithSeasonNumber(2).WithNumber(3).Build();
        Episode tvdbEpisode4 = new TvdbEpisodeDataBuilder().WithId(204).WithSeasonNumber(2).WithNumber(4).Build();
        Episode tvdbEpisode5 = new TvdbEpisodeDataBuilder().WithId(205).WithSeasonNumber(2).WithNumber(5).Build();
        Episode tvdbEpisode6 = new TvdbEpisodeDataBuilder().WithId(206).WithSeasonNumber(2).WithNumber(6).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000)
            .WithEpisodes(tvdbEpisode1, tvdbEpisode2, tvdbEpisode3, tvdbEpisode4, tvdbEpisode5, tvdbEpisode6)
            .Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[4].TvdbEpisodes[0].TvdbId.ShouldBe(205);
        program.Episodes[5].TvdbEpisodes[0].TvdbId.ShouldBe(206);
    }
}
