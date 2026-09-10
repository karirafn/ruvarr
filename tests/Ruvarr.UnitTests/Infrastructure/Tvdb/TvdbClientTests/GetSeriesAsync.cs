using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetSeriesAsync
{
    // NFD: o + U+0308 (COMBINING DIAERESIS), a + U+0301 (COMBINING ACUTE ACCENT)
    // Written with explicit \u escapes so git/editor normalization cannot collapse them.
    private const string NfdSeriesName = "Skjaldb\u006F\u0308kustr\u0061\u0301kur";
    private const string NfcSeriesName = "Skjaldb\u00F6kustr\u00E1kur";

    [Fact]
    public async Task WhenResponseIs200_ReturnsUnwrappedSeriesData()
    {
        // Arrange
        string responseBody = """
            {
                "status": "success",
                "data": {
                    "series": {
                        "id": 42,
                        "name": "Test Series",
                        "slug": "test-series",
                        "image": "http://example.com/img.jpg",
                        "nameTranslations": [],
                        "overviewTranslations": [],
                        "episodes": [],
                        "aliases": [],
                        "firstAired": "2020-01-01",
                        "lastAired": "2024-01-01",
                        "nextAired": "",
                        "score": 0,
                        "status": { "id": 1, "name": "Continuing", "recordType": "series", "keepUpdated": true },
                        "originalCountry": "is",
                        "originalLanguage": "isl",
                        "isOrderRandomized": false,
                        "lastUpdated": "2024-01-01",
                        "averageRuntime": 30,
                        "overview": "",
                        "year": "2020"
                    },
                    "episodes": []
                },
                "links": { "self": "http://example.com", "total_items": 1, "page_size": 10 }
            }
            """;

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(42, TestContext.Current.CancellationToken);

        // Assert
        SeriesData data = result.ShouldNotBeNull();
        data.Series.Id.ShouldBe(42);
        data.Series.Name.ShouldBe("Test Series");
    }

    [Fact]
    public async Task WhenResponseIsNonSuccess_ReturnsNull()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(99, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenResponseBodyContainsNfdString_DeserializesToNfc()
    {
        // Arrange
        string responseBody = "{\"status\":\"success\"," +
            "\"data\":{\"series\":{\"id\":1,\"name\":\"" + NfdSeriesName + "\"," +
            "\"slug\":\"test\",\"image\":\"http://example.com/img.jpg\"," +
            "\"nameTranslations\":[],\"overviewTranslations\":[]," +
            "\"episodes\":[],\"aliases\":[]," +
            "\"firstAired\":\"2020-01-01\",\"lastAired\":\"2024-01-01\"," +
            "\"nextAired\":\"\",\"score\":0," +
            "\"status\":{\"id\":1,\"name\":\"Continuing\",\"recordType\":\"series\",\"keepUpdated\":true}," +
            "\"originalCountry\":\"is\",\"originalLanguage\":\"isl\"," +
            "\"isOrderRandomized\":false,\"lastUpdated\":\"2024-01-01\"," +
            "\"averageRuntime\":30,\"overview\":\"\",\"year\":\"2020\"}," +
            "\"episodes\":[]}," +
            "\"links\":{\"self\":\"http://example.com\",\"total_items\":1,\"page_size\":10}}";

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(1, TestContext.Current.CancellationToken);

        // Assert
        SeriesData data = result.ShouldNotBeNull();
        data.Series.Name.ShouldBe(NfcSeriesName);
    }

    [Fact]
    public async Task WhenResponseHasMultiplePages_ReturnsAllEpisodesConcatenatedInOrder()
    {
        // Arrange
        string page0Body = """
            {
                "status": "success",
                "data": {
                    "series": {
                        "id": 10,
                        "name": "Long Series",
                        "slug": "long-series",
                        "image": "http://example.com/img.jpg",
                        "nameTranslations": [],
                        "overviewTranslations": [],
                        "episodes": [],
                        "aliases": [],
                        "firstAired": "2000-01-01",
                        "lastAired": "2024-01-01",
                        "nextAired": "",
                        "score": 0,
                        "status": { "id": 1, "name": "Continuing", "recordType": "series", "keepUpdated": true },
                        "originalCountry": "us",
                        "originalLanguage": "eng",
                        "isOrderRandomized": false,
                        "lastUpdated": "2024-01-01",
                        "averageRuntime": 22,
                        "overview": "",
                        "year": "2000"
                    },
                    "episodes": [{ "id": 1, "seriesId": 10, "name": "Ep1", "overview": "", "aired": "2000-01-01", "runtime": null, "nameTranslations": [], "overviewTranslations": [], "image": "http://example.com/e1.jpg", "imageType": null, "isMovie": 0, "number": 1, "absoluteNumber": 1, "seasonNumber": 1, "lastUpdated": "2024-01-01", "finaleType": null, "year": "2000", "airsAfterSeason": 0, "airsBeforeSeason": 0, "airsBeforeEpisode": 0 }]
                },
                "links": { "self": "http://example.com", "next": "http://example.com?page=1", "total_items": 2, "page_size": 1 }
            }
            """;

        string page1Body = """
            {
                "status": "success",
                "data": {
                    "series": {
                        "id": 10,
                        "name": "Long Series",
                        "slug": "long-series",
                        "image": "http://example.com/img.jpg",
                        "nameTranslations": [],
                        "overviewTranslations": [],
                        "episodes": [],
                        "aliases": [],
                        "firstAired": "2000-01-01",
                        "lastAired": "2024-01-01",
                        "nextAired": "",
                        "score": 0,
                        "status": { "id": 1, "name": "Continuing", "recordType": "series", "keepUpdated": true },
                        "originalCountry": "us",
                        "originalLanguage": "eng",
                        "isOrderRandomized": false,
                        "lastUpdated": "2024-01-01",
                        "averageRuntime": 22,
                        "overview": "",
                        "year": "2000"
                    },
                    "episodes": [{ "id": 2, "seriesId": 10, "name": "Ep2", "overview": "", "aired": "2000-01-08", "runtime": null, "nameTranslations": [], "overviewTranslations": [], "image": "http://example.com/e2.jpg", "imageType": null, "isMovie": 0, "number": 2, "absoluteNumber": 2, "seasonNumber": 1, "lastUpdated": "2024-01-01", "finaleType": null, "year": "2000", "airsAfterSeason": 0, "airsBeforeSeason": 0, "airsBeforeEpisode": 0 }]
                },
                "links": { "self": "http://example.com?page=1", "total_items": 2, "page_size": 1 }
            }
            """;

        Dictionary<int, string> pageResponses = new() { [0] = page0Body, [1] = page1Body };
        using HttpClient httpClient = CreatePagedHttpClient(HttpStatusCode.OK, pageResponses);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(10, TestContext.Current.CancellationToken);

        // Assert
        SeriesData data = result.ShouldNotBeNull();
        data.Episodes.Count.ShouldBe(2);
        data.Episodes[0].Id.ShouldBe(1);
        data.Episodes[1].Id.ShouldBe(2);
    }

    [Fact]
    public async Task WhenLinksNextIsNull_MakesExactlyOneRequest()
    {
        // Arrange
        string responseBody = """
            {
                "status": "success",
                "data": {
                    "series": {
                        "id": 5,
                        "name": "Short Series",
                        "slug": "short-series",
                        "image": "http://example.com/img.jpg",
                        "nameTranslations": [],
                        "overviewTranslations": [],
                        "episodes": [],
                        "aliases": [],
                        "firstAired": "2020-01-01",
                        "lastAired": "2024-01-01",
                        "nextAired": "",
                        "score": 0,
                        "status": { "id": 1, "name": "Continuing", "recordType": "series", "keepUpdated": true },
                        "originalCountry": "is",
                        "originalLanguage": "isl",
                        "isOrderRandomized": false,
                        "lastUpdated": "2024-01-01",
                        "averageRuntime": 30,
                        "overview": "",
                        "year": "2020"
                    },
                    "episodes": [{ "id": 7, "seriesId": 5, "name": "Only", "overview": "", "aired": "2020-01-01", "runtime": null, "nameTranslations": [], "overviewTranslations": [], "image": "http://example.com/e7.jpg", "imageType": null, "isMovie": 0, "number": 1, "absoluteNumber": 1, "seasonNumber": 1, "lastUpdated": "2024-01-01", "finaleType": null, "year": "2020", "airsAfterSeason": 0, "airsBeforeSeason": 0, "airsBeforeEpisode": 0 }]
                },
                "links": { "self": "http://example.com", "total_items": 1, "page_size": 500 }
            }
            """;

        using CountingHandler countingHandler = new(HttpStatusCode.OK, responseBody);
        using HttpClient httpClient = CreateCountingHttpClient(countingHandler);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(5, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        countingHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task WhenLinksNextIsAlwaysNonNull_StopsAtPageCap()
    {
        // Arrange — handler always returns next != null, so without a cap the loop would be infinite
        string infiniteBody = """
            {
                "status": "success",
                "data": {
                    "series": {
                        "id": 99,
                        "name": "Infinite Series",
                        "slug": "infinite-series",
                        "image": "http://example.com/img.jpg",
                        "nameTranslations": [],
                        "overviewTranslations": [],
                        "episodes": [],
                        "aliases": [],
                        "firstAired": "2000-01-01",
                        "lastAired": "2024-01-01",
                        "nextAired": "",
                        "score": 0,
                        "status": { "id": 1, "name": "Continuing", "recordType": "series", "keepUpdated": true },
                        "originalCountry": "us",
                        "originalLanguage": "eng",
                        "isOrderRandomized": false,
                        "lastUpdated": "2024-01-01",
                        "averageRuntime": 22,
                        "overview": "",
                        "year": "2000"
                    },
                    "episodes": []
                },
                "links": { "self": "http://example.com", "next": "http://example.com?page=999", "total_items": 99999, "page_size": 500 }
            }
            """;

        using CountingHandler countingHandler = new(HttpStatusCode.OK, infiniteBody);
        using HttpClient httpClient = CreateCountingHttpClient(countingHandler);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(99, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        countingHandler.RequestCount.ShouldBeLessThanOrEqualTo(TvdbClient.MaxPageCount);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        StaticHandler handler = new(statusCode, responseBody);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://tvdb.localhost")
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreatePagedHttpClient(HttpStatusCode statusCode, Dictionary<int, string> pageResponses)
    {
        PagedHandler handler = new(statusCode, pageResponses);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://tvdb.localhost")
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "CountingHandler lifetime is managed by the caller")]
    private static HttpClient CreateCountingHttpClient(CountingHandler handler) =>
        new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://tvdb.localhost")
        };

    private sealed class StaticHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }

    private sealed class PagedHandler(HttpStatusCode statusCode, Dictionary<int, string> pageResponses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int page = 0;
            string? query = request.RequestUri?.Query;
            if (query is not null)
            {
                System.Collections.Specialized.NameValueCollection parsed = System.Web.HttpUtility.ParseQueryString(query);
                if (int.TryParse(parsed["page"], out int parsedPage))
                {
                    page = parsedPage;
                }
            }

            string body = pageResponses.TryGetValue(page, out string? pageBody)
                ? pageBody
                : """{"status":"error","data":null,"links":{"self":"http://example.com","total_items":0,"page_size":500}}""";

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class CountingHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }

    }
}
