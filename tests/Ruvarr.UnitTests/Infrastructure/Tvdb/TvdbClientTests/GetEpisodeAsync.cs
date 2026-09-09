using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetEpisodeAsync
{
    [Fact]
    public async Task WhenResponseIs200_ReturnsUnwrappedEpisode()
    {
        // Arrange
        string responseBody = """
            {
                "status": "success",
                "Data": {
                    "id": 7,
                    "seriesId": 42,
                    "name": "Pilot",
                    "overview": "The first episode",
                    "aired": "2020-01-01",
                    "runtime": 45,
                    "nameTranslations": [],
                    "overviewTranslations": [],
                    "image": "http://example.com/ep.jpg",
                    "imageType": 1,
                    "isMovie": 0,
                    "number": 1,
                    "absoluteNumber": 1,
                    "seasonNumber": 1,
                    "lastUpdated": "2024-01-01",
                    "finaleType": null,
                    "year": "2020",
                    "airsAfterSeason": 0,
                    "airsBeforeSeason": 0,
                    "airsBeforeEpisode": 0
                }
            }
            """;

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        Episode? result = await sut.GetEpisodeAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Episode episode = result.ShouldNotBeNull();
        episode.Id.ShouldBe(7);
        episode.Name.ShouldBe("Pilot");
    }

    [Fact]
    public async Task WhenResponseIsNonSuccess_ReturnsNull()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        Episode? result = await sut.GetEpisodeAsync(99, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
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
