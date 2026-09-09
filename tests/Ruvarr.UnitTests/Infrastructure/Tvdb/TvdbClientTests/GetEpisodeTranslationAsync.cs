using System.Net;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetEpisodeTranslationAsync : IDisposable
{
    private const string ApiKey = "test-api-key";
    private const string AccessToken = "fake-access-token";

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 64 });

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task WhenResponseIs200_ReturnsUnwrappedTranslation()
    {
        // Arrange
        string responseBody = """
            {
                "status": "success",
                "Data": {
                    "name": "Tilraunir",
                    "overview": "Fyrsti þátturinn",
                    "language": "isl",
                    "isPrimary": true
                }
            }
            """;

        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        EpisodeTranslation? result = await sut.GetEpisodeTranslationAsync(7, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        EpisodeTranslation translation = result.ShouldNotBeNull();
        translation.Name.ShouldBe("Tilraunir");
        translation.Language.ShouldBe("isl");
    }

    [Fact]
    public async Task WhenResponseIsNonSuccess_ReturnsNull()
    {
        // Arrange
        ISettingsStore settingsStore = CreateSettingsStore();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        using TvdbClient sut = new(httpClient, _cache, settingsStore);

        // Act
        EpisodeTranslation? result = await sut.GetEpisodeTranslationAsync(99, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
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
