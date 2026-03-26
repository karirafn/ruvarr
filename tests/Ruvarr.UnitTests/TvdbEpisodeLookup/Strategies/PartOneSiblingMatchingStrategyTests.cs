using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Strategies;

using Shouldly;

namespace Ruvarr.UnitTests.TvdbEpisodeLookup.Strategies;

public sealed class PartOneSiblingMatchingStrategyTests
{
    private static PartOneSiblingMatchingStrategy CreateSut() => new(
        NullLogger<PartOneSiblingMatchingStrategy>.Instance);

    [Fact]
    public async Task MatchesPartTwoWhenPartOneIsMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(501, 1, 3, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501).WithName("Christmas Lads Part 1").WithSeasonNumber(1).WithNumber(3).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502).WithName("Christmas Lads Part 2").WithSeasonNumber(1).WithNumber(4).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(502);
        program.Episodes[1].TvdbEpisodes[0].SeasonNumber.ShouldBe(1);
        program.Episodes[1].TvdbEpisodes[0].EpisodeNumber.ShouldBe(4);
    }

    [Fact]
    public async Task MatchesPartTwoWithParenthesesFormat()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar - fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar - síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(601, 2, 5, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(601).WithName("Christmas Lads (1)").WithSeasonNumber(2).WithNumber(5).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(602).WithName("Christmas Lads (2)").WithSeasonNumber(2).WithNumber(6).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(602);
        program.Episodes[1].TvdbEpisodes[0].SeasonNumber.ShouldBe(2);
        program.Episodes[1].TvdbEpisodes[0].EpisodeNumber.ShouldBe(6);
    }

    [Fact]
    public async Task SkipsWhenPartOneSiblingIsNotMatched()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsWhenNoCorrespondingTvdbPartTwoEpisodeExists()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(501, 1, 3, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501).WithName("Christmas Lads Part 1").WithSeasonNumber(1).WithNumber(3).Build();
        Episode tvdbEpisodeUnrelated = new TvdbEpisodeDataBuilder()
            .WithId(503).WithName("Something Else").WithSeasonNumber(1).WithNumber(4).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisodeUnrelated).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsWhenTvdbPartOneEpisodeHasNoPartSuffix()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, síðari hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(501, 1, 3, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501).WithName("Christmas Lads").WithSeasonNumber(1).WithNumber(3).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502).WithName("Christmas Lads Continued").WithSeasonNumber(1).WithNumber(4).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchesPartTwoWithSeinniHluti()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Jólasveinar, fyrri hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.TryAddEpisode("ep0002", new Uri("http://test.com"), "Jólasveinar, seinni hluti", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        program.Episodes[0].Match(501, 1, 3, false);

        Episode tvdbEpisode1 = new TvdbEpisodeDataBuilder()
            .WithId(501).WithName("Christmas Lads Part 1").WithSeasonNumber(1).WithNumber(3).Build();
        Episode tvdbEpisode2 = new TvdbEpisodeDataBuilder()
            .WithId(502).WithName("Christmas Lads Part 2").WithSeasonNumber(1).WithNumber(4).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode1, tvdbEpisode2).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(502);
        program.Episodes[1].TvdbEpisodes[0].SeasonNumber.ShouldBe(1);
        program.Episodes[1].TvdbEpisodes[0].EpisodeNumber.ShouldBe(4);
    }

    [Fact]
    public async Task SkipsNonPartTwoEpisodes()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Show").Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Regular Episode", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();

        EpisodeMatchingContext context = new(program, seriesData, []);
        PartOneSiblingMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes.ShouldBeEmpty();
    }
}
