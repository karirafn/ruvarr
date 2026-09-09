using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbClientTests;

public sealed class GetEpisodeTranslationAsync
{
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

        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, responseBody);
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

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
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        TvdbClient sut = new(NullLogger<TvdbClient>.Instance, httpClient);

        // Act
        EpisodeTranslation? result = await sut.GetEpisodeTranslationAsync(99, cancellationToken: TestContext.Current.CancellationToken);

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
