using System.Net;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetSeriesAsync : IDisposable
{
    private const string ApiKey = "test-api-key";
    private const string AccessToken = "fake-access-token";

    // NFD: o + U+0308 (COMBINING DIAERESIS), a + U+0301 (COMBINING ACUTE ACCENT)
    // Written with explicit \u escapes so git/editor normalization cannot collapse them.
    private const string NfdSeriesName = "Skjaldb\u006f\u0308kustr\u0061\u0301kur";
    private const string NfcSeriesName = "Skjaldb\u00f6kustr\u00e1kur";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 });

    public void Dispose() => _cache.Dispose();

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

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(42, TestContext.Current.CancellationToken);

        // Assert
        SeriesData data = result.ShouldNotBeNull();
        data.Series.Id.ShouldBe(42);
        data.Series.Name.ShouldBe("Test Series");
    }

    [Fact]
    public async Task WhenResponseIsNonSuccess_ThrowsHttpRequestException()
    {
        // Arrange
        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act / Assert
        await Should.ThrowAsync<HttpRequestException>(
            () => sut.GetSeriesAsync(99, TestContext.Current.CancellationToken));
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

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        SeriesData? result = await sut.GetSeriesAsync(1, TestContext.Current.CancellationToken);

        // Assert
        SeriesData data = result.ShouldNotBeNull();
        data.Series.Name.ShouldBe(NfcSeriesName);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode getStatusCode, string getResponseBody)
    {
        string loginResponseBody = "{\"data\":{\"token\":\"" + AccessToken + "\"},\"status\":\"success\"}";
        RoutingHandler handler = new(loginResponseBody, getStatusCode, getResponseBody);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://tvdb.localhost")
        };
    }

    private static ISettingsStore CreateSettingsStore()
    {
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey));
        return settingsStore;
    }

    private sealed class RoutingHandler(string loginResponseBody, HttpStatusCode getStatusCode, string getResponseBody)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool isLogin = request.Method == HttpMethod.Post
                && (request.RequestUri?.AbsolutePath.Contains("login", StringComparison.Ordinal) ?? false);

            if (isLogin)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(loginResponseBody, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(getStatusCode)
            {
                Content = new StringContent(getResponseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
