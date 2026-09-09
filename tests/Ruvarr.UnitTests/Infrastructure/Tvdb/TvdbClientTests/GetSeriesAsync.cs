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
    private const string NfdSeriesName = "Skjaldbökustrákur";
    private const string NfcSeriesName = "Skjaldbökustrákur";

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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        StaticHandler handler = new(statusCode, responseBody);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://tvdb.localhost")
        };
    }

    private sealed class StaticHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }
}
