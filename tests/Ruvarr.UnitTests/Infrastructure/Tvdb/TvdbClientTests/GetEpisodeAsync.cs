using System.Net;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetEpisodeAsync : IDisposable
{
    private const string ApiKey = "test-api-key";
    private const string AccessToken = "fake-access-token";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 });

    public void Dispose() => _cache.Dispose();

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

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        Episode? result = await sut.GetEpisodeAsync(7, TestContext.Current.CancellationToken);

        // Assert
        Episode episode = result.ShouldNotBeNull();
        episode.Id.ShouldBe(7);
        episode.Name.ShouldBe("Pilot");
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
            () => sut.GetEpisodeAsync(99, TestContext.Current.CancellationToken));
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
