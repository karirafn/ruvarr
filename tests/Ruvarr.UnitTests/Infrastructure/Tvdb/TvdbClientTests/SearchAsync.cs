using System.Net;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class SearchAsync : IDisposable
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
    public async Task WhenResponseIs200_ReturnsDeserializedSearchResponse()
    {
        // Arrange
        string searchBody = """
            {
                "status": "success",
                "data": [],
                "links": { "self": "http://example.com", "total_items": 0, "page_size": 10 }
            }
            """;

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(searchBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        SearchResponse result = await sut.SearchAsync(query: "test", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Status.ShouldBe("success");
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenResponseBodyIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient("null");
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.SearchAsync(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        ex.Message.ShouldBe("Failed to search the TVDB");
    }

    [Fact]
    public async Task WhenResponseBodyContainsNfdString_DeserializesToNfc()
    {
        // Arrange
        string searchBody = "{\"status\":\"success\"," +
            "\"data\":[{\"objectID\":\"s1\",\"aliases\":[]," +
            "\"country\":\"is\",\"Id\":\"1\"," +
            "\"image_url\":\"http://example.com/img.jpg\"," +
            "\"name\":\"" + NfdSeriesName + "\"," +
            "\"first_air_time\":\"2020-01-01\",\"overview\":\"\"," +
            "\"primary_language\":\"isl\",\"primary_type\":\"series\"," +
            "\"status\":\"continuing\",\"Type\":\"series\",\"tvdb_id\":\"1\"," +
            "\"year\":\"2020\",\"slug\":\"test\",\"overviews\":{}," +
            "\"translations\":{},\"network\":\"RUV\",\"remote_ids\":[]}]," +
            "\"links\":{\"self\":\"http://example.com\",\"total_items\":1,\"page_size\":10}}";

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(searchBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        SearchResponse result = await sut.SearchAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe(NfcSeriesName);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(string searchResponseBody)
    {
        string loginResponseBody = "{\"data\":{\"token\":\"" + AccessToken + "\"},\"status\":\"success\"}";
        RoutingHandler handler = new(loginResponseBody, searchResponseBody);
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

    private sealed class RoutingHandler(string loginResponseBody, string getResponseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool isLogin = request.Method == HttpMethod.Post
                && (request.RequestUri?.AbsolutePath.Contains("login", StringComparison.Ordinal) ?? false);

            string responseBody = isLogin ? loginResponseBody : getResponseBody;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
