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
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(101, 1, 1, false);

        Episode tvdbEpisode202 = new TvdbEpisodeDataBuilder()
            .WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode202).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
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
    public async Task SkipsWhenOtherSeasonHasSameEpisodeCount()
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
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }
}
