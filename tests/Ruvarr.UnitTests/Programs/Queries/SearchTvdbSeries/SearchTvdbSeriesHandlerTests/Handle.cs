using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Queries.SearchTvdbSeries;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Queries.SearchTvdbSeries.SearchTvdbSeriesHandlerTests;

public sealed class Handle : IDisposable
{
    private readonly ITvdbClient _tvdbClient = Substitute.For<ITvdbClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SearchTvdbSeriesHandler _sut;

    public Handle()
    {
        _sut = new SearchTvdbSeriesHandler(_tvdbClient, _cache);
    }

    [Fact]
    public async Task ReturnsOnlySeriesTypeResults()
    {
        // Arrange
        SearchResponse response = CreateSearchResponse(
            CreateDatum(tvdbId: "100", name: "Test Series", type: "series", year: "2020"),
            CreateDatum(tvdbId: "200", name: "Test Movie", type: "movie", year: "2021"));

        _tvdbClient.SearchAsync(query: "Test", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("Test"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Test Series");
    }

    [Fact]
    public async Task MapsFieldsCorrectly()
    {
        // Arrange
        SearchResponse response = CreateSearchResponse(
            CreateDatum(tvdbId: "42", name: "Kastljós", type: "series", year: "2015"));

        _tvdbClient.SearchAsync(query: "Kastljós", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("Kastljós"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].TvdbId.ShouldBe(42);
        result[0].Name.ShouldBe("Kastljós");
        result[0].Year.ShouldBe("2015");
    }

    [Fact]
    public async Task ReturnsEmptyListWhenNoResults()
    {
        // Arrange
        SearchResponse response = CreateSearchResponse();

        _tvdbClient.SearchAsync(query: "NonExistent", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("NonExistent"), CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SkipsEntriesWithUnparseableTvdbId()
    {
        // Arrange
        SearchResponse response = CreateSearchResponse(
            CreateDatum(tvdbId: "not-a-number", name: "Bad Entry", type: "series", year: "2020"),
            CreateDatum(tvdbId: "100", name: "Good Entry", type: "series", year: "2021"));

        _tvdbClient.SearchAsync(query: "Test", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("Test"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Good Entry");
    }

    [Fact]
    public async Task LimitsToTwentyResults()
    {
        // Arrange
        Datum[] data = Enumerable.Range(1, 25)
            .Select(i => CreateDatum(tvdbId: i.ToString(System.Globalization.CultureInfo.InvariantCulture), name: $"Series {i}", type: "series", year: "2020"))
            .ToArray();

        SearchResponse response = CreateSearchResponse(data);

        _tvdbClient.SearchAsync(query: "Test", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("Test"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(20);
    }

    [Fact]
    public async Task CachesResultsForFiveMinutes()
    {
        // Arrange
        SearchResponse response = CreateSearchResponse(
            CreateDatum(tvdbId: "100", name: "Cached Series", type: "series", year: "2020"));

        _tvdbClient.SearchAsync(query: "Test", cancellationToken: Arg.Any<CancellationToken>()).Returns(response);

        // Act
        await _sut.Handle(new SearchTvdbSeriesQuery("Test"), CancellationToken.None);
        IReadOnlyList<TvdbSeriesSuggestion> result = await _sut.Handle(new SearchTvdbSeriesQuery("Test"), CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        await _tvdbClient.Received(1).SearchAsync(query: "Test", cancellationToken: Arg.Any<CancellationToken>());
    }

    public void Dispose() => _cache.Dispose();

    private static SearchResponse CreateSearchResponse(params Datum[] data)
    {
        return new SearchResponse("success", data, new Links(null, new Uri("http://test.com"), null, 0, 0));
    }

    private static Datum CreateDatum(string tvdbId, string name, string type, string year)
    {
        return new Datum(
            ObjectId: $"series-{tvdbId}",
            Aliases: [],
            Country: "usa",
            Id: tvdbId,
            ImageUrl: new Uri("http://test.com"),
            Name: name,
            FirstAirDate: "",
            Overview: "",
            PrimaryLanguage: "eng",
            PrimaryType: type,
            Status: "Continuing",
            Type: type,
            TvdbId: tvdbId,
            Year: year,
            Slug: name.Replace(' ', '-'),
            Overviews: new Dictionary<string, string>(),
            Translations: new Dictionary<string, string>(),
            Network: "",
            RemoteIds: []);
    }
}
