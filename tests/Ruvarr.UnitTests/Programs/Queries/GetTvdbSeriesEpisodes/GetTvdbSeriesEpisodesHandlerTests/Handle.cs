using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Queries.GetTvdbSeriesEpisodes.GetTvdbSeriesEpisodesHandlerTests;

public sealed class Handle : IDisposable
{
    private readonly ITvdbClient _tvdbClient = Substitute.For<ITvdbClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly GetTvdbSeriesEpisodesHandler _sut;

    public Handle()
    {
        _sut = new GetTvdbSeriesEpisodesHandler(_tvdbClient, _cache);
    }

    [Fact]
    public async Task ReturnsEpisodesFromTvdbClient()
    {
        // Arrange
        SeriesData seriesData = CreateSeriesData(
            CreateEpisode(id: 100, name: "Pilot", seasonNumber: 1, number: 1),
            CreateEpisode(id: 101, name: "Second", seasonNumber: 1, number: 2));

        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>()).Returns(seriesData);

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(2);
        result[0].ShouldBe(new TvdbSeriesEpisode(100, "Pilot", 1, 1));
        result[1].ShouldBe(new TvdbSeriesEpisode(101, "Second", 1, 2));
    }

    [Fact]
    public async Task FiltersOutSeason0Episodes()
    {
        // Arrange
        SeriesData seriesData = CreateSeriesData(
            CreateEpisode(id: 100, name: "Special", seasonNumber: 0, number: 1),
            CreateEpisode(id: 101, name: "Pilot", seasonNumber: 1, number: 1));

        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>()).Returns(seriesData);

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].TvdbId.ShouldBe(101);
    }

    [Fact]
    public async Task ReturnsSortedBySeasonThenEpisode()
    {
        // Arrange
        SeriesData seriesData = CreateSeriesData(
            CreateEpisode(id: 103, name: "S2E1", seasonNumber: 2, number: 1),
            CreateEpisode(id: 101, name: "S1E2", seasonNumber: 1, number: 2),
            CreateEpisode(id: 100, name: "S1E1", seasonNumber: 1, number: 1));

        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>()).Returns(seriesData);

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);

        // Assert
        result[0].TvdbId.ShouldBe(100);
        result[1].TvdbId.ShouldBe(101);
        result[2].TvdbId.ShouldBe(103);
    }

    [Fact]
    public async Task ReturnsEmptyListWhenSeriesDataIsNull()
    {
        // Arrange
        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsCachedResultOnSecondCall()
    {
        // Arrange
        SeriesData seriesData = CreateSeriesData(
            CreateEpisode(id: 100, name: "Pilot", seasonNumber: 1, number: 1));

        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>()).Returns(seriesData);

        // Act
        await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);
        IReadOnlyList<TvdbSeriesEpisode> result = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        await _tvdbClient.Received(1).GetSeriesAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DifferentSeriesIdsAreNotCachedTogether()
    {
        // Arrange
        _tvdbClient.GetSeriesAsync(42, Arg.Any<CancellationToken>())
            .Returns(CreateSeriesData(CreateEpisode(id: 100, name: "A", seasonNumber: 1, number: 1)));
        _tvdbClient.GetSeriesAsync(99, Arg.Any<CancellationToken>())
            .Returns(CreateSeriesData(CreateEpisode(id: 200, name: "B", seasonNumber: 1, number: 1)));

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result1 = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(42), CancellationToken.None);
        IReadOnlyList<TvdbSeriesEpisode> result2 = await _sut.Handle(new GetTvdbSeriesEpisodesQuery(99), CancellationToken.None);

        // Assert
        result1[0].TvdbId.ShouldBe(100);
        result2[0].TvdbId.ShouldBe(200);
    }

    public void Dispose() => _cache.Dispose();

    private static SeriesData CreateSeriesData(params Episode[] episodes)
    {
        Series series = new(
            Id: 1, Name: "Test", Slug: "test", Image: new Uri("http://test.com"),
            NameTranslations: [], OverviewTranslations: [], Episodes: [],
            Aliases: [], FirstAired: "", LastAired: "", NextAired: "",
            Score: 0, Status: new Status(0, "", "", false),
            OriginalCountry: "", OriginalLanguage: "", IsOrderRandomized: false,
            LastUpdated: "", AverageRuntime: null, Overview: "", Year: "");

        return new SeriesData(series, episodes);
    }

    private static Episode CreateEpisode(int id, string name, int seasonNumber, int number) => new(
        Id: id, SeriesId: 1, Name: name, Overview: "", Aired: "", Runtime: null,
        NameTranslations: [], OverviewTranslations: [], Image: new Uri("http://test.com"),
        ImageType: null, IsMovie: 0, Number: number, AbsoluteNumber: 0,
        SeasonNumber: seasonNumber, LastUpdated: "", FinaleType: null, Year: "",
        AirsAfterSeason: 0, AirsBeforeSeason: 0, AirsBeforeEpisode: 0);
}
