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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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
        program.Episodes[0].Match(201, 2, 1, SonarrStatuses.NotTracked, MatchOrigin.Automatic);

        Episode tvdbEpisode201 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode tvdbEpisode202 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder()
            .WithId(1000).WithEpisodes(tvdbEpisode201, tvdbEpisode202).Build();

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
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
        program.Episodes[0].Match(201, 2, 1, SonarrStatuses.NotTracked, MatchOrigin.Automatic);
        program.Episodes[1].Match(202, 2, 2, SonarrStatuses.NotTracked, MatchOrigin.Automatic);
        program.Episodes[2].Match(203, 2, 3, SonarrStatuses.NotTracked, MatchOrigin.Automatic);
        program.Episodes[3].Match(204, 2, 4, SonarrStatuses.NotTracked, MatchOrigin.Automatic);

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

        EpisodeMatchingContext context = new(program, seriesData, SonarrStatuses.NotTracked);
        EpisodeNumberMatchingStrategy sut = CreateSut();

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        program.Episodes[0].TvdbEpisodes[0].TvdbId.ShouldBe(201);
        program.Episodes[1].TvdbEpisodes[0].TvdbId.ShouldBe(202);
        program.Episodes[2].TvdbEpisodes[0].TvdbId.ShouldBe(203);
        program.Episodes[3].TvdbEpisodes[0].TvdbId.ShouldBe(204);
        program.Episodes[4].TvdbEpisodes[0].TvdbId.ShouldBe(205);
        program.Episodes[5].TvdbEpisodes[0].TvdbId.ShouldBe(206);
    }

    private static RuvProgram MatchedProgram(string name, int published, string titleSuffix)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName(name).WithForeignName(null).Build();
        for (int n = 1; n <= published; n++)
        {
            program.TryAddEpisode($"ep{n:D4}", new Uri("http://test.com"), $"Þáttur {n}{titleSuffix}", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        }

        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        return program;
    }

    private static IEnumerable<Episode> TvdbSeason(int season, int count) =>
        Enumerable.Range(1, count).Select(n =>
            new TvdbEpisodeDataBuilder().WithId((season * 1000) + n).WithSeasonNumber(season).WithNumber(n).Build());

    private static async Task MatchAsync(RuvProgram program, params IEnumerable<Episode>[] seasons)
    {
        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).WithEpisodes([.. seasons.SelectMany(x => x)]).Build();
        await CreateSut().MatchAsync(new EpisodeMatchingContext(program, seriesData, SonarrStatuses.NotTracked), CancellationToken.None);
    }

    [Fact]
    public async Task OneManualSeason_MatchesByNumberInThatSeason()
    {
        // Arrange: Ráðherrann, manual seasons {2}, TVDB S1 and S2 both have 8 episodes
        RuvProgram program = MatchedProgram("Ráðherrann", 8, " af 8");
        program.SetManualSeasons([2], [1, 2]);

        // Act
        await MatchAsync(program, TvdbSeason(1, 8), TvdbSeason(2, 8));

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Single().SeasonNumber == 2);
        program.Episodes[7].TvdbEpisodes[0].EpisodeNumber.ShouldBe(8);
    }

    [Fact]
    public async Task PartlyPublishedSeason_MatchesAgainstTheExpectedSeasonLength()
    {
        // Arrange: Molang VI, 49 of 52 published
        RuvProgram program = MatchedProgram("Molang VI", 49, " af 52");

        // Act
        await MatchAsync(program, TvdbSeason(5, 52), TvdbSeason(6, 52));

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Single().SeasonNumber == 6);
    }

    [Fact]
    public async Task ManualSeasonOfADifferentLength_DoesNotMatchByNumber()
    {
        // Arrange: Andri og Edda II, manual seasons {3}; II's "Þáttur 1" is S3E11
        RuvProgram program = MatchedProgram("Andri og Edda II", 10, " af 10");
        program.SetManualSeasons([3], [1, 2, 3]);

        // Act
        await MatchAsync(program, TvdbSeason(1, 10), TvdbSeason(2, 10), TvdbSeason(3, 20));

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Count == 0);
    }

    [Fact]
    public async Task SeveralManualSeasons_DoNotMatchByNumber()
    {
        // Arrange
        RuvProgram program = MatchedProgram("Bursti I", 8, " af 8");
        program.SetManualSeasons([1, 2], [1, 2]);

        // Act
        await MatchAsync(program, TvdbSeason(1, 8), TvdbSeason(2, 8));

        // Assert
        program.Episodes.ShouldAllBe(x => x.TvdbEpisodes.Count == 0);
    }
}
