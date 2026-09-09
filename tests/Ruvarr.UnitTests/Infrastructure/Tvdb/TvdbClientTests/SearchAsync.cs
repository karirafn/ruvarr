using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class SearchAsync
{
    // NFD: o + U+0308 (COMBINING DIAERESIS), a + U+0301 (COMBINING ACUTE ACCENT)
    // Written with explicit \u escapes so git/editor normalization cannot collapse them.
    private const string NfdSeriesName = "Skjaldbökustrákur";
    private const string NfcSeriesName = "Skjaldbökustrákur";

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

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, searchBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

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
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, "null");
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

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

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, searchBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        SearchResponse result = await sut.SearchAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].Name.ShouldBe(NfcSeriesName);
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
