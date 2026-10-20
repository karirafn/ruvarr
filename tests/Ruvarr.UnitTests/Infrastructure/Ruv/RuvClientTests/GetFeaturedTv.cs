using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv.RuvClientTests;

public sealed class GetFeaturedTv
{
    [Fact]
    public async Task WhenPayloadContainsEpisodeWithNullRating_ReturnsResultWithoutThrowing()
    {
        // Arrange
        string fixture = LoadFixture("featured-tv-sample.json");
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, fixture);
        RuvClient sut = new(NullLogger<RuvClient>.Instance, httpClient);

        // Act
        RuvFeaturedTv? result = await sut.GetFeaturedTv(TestContext.Current.CancellationToken);

        // Assert
        RuvFeaturedTv featured = result.ShouldNotBeNull();
        featured.Panels.ShouldNotBeEmpty();
        featured.Panels[0].Programs.ShouldNotBeEmpty();
        featured.Panels[0].Programs[0].Episodes.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task WhenPayloadContainsProgramWithNullForeignTitle_ForeignTitleIsNull()
    {
        // Arrange
        string fixture = LoadFixture("featured-tv-sample.json");
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, fixture);
        RuvClient sut = new(NullLogger<RuvClient>.Instance, httpClient);

        // Act
        RuvFeaturedTv? result = await sut.GetFeaturedTv(TestContext.Current.CancellationToken);

        // Assert
        RuvFeaturedTv featured = result.ShouldNotBeNull();
        RuvTvProgram program = featured.Panels[0].Programs[0];
        program.ForeignTitle.ShouldBeNull();
    }

    [Fact]
    public async Task WhenPayloadContainsEpisodeWithNullFirstRun_ThrowsJsonExceptionNamingPath()
    {
        // Arrange
        string fixture = LoadFixture("featured-tv-firstrun-null.json");
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, fixture);
        RuvClient sut = new(NullLogger<RuvClient>.Instance, httpClient);

        // Act & Assert
        JsonException ex = await Should.ThrowAsync<JsonException>(
            async () => await sut.GetFeaturedTv(TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("firstrun");
        string path = ex.Path.ShouldNotBeNull();
        path.ShouldContain("firstrun");
    }

    private static string LoadFixture(string filename)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"Ruvarr.UnitTests.Infrastructure.Ruv.Fixtures.{filename}";
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        stream.ShouldNotBeNull($"Embedded resource '{resourceName}' not found in assembly.");
        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        StaticHandler handler = new(statusCode, responseBody);
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri("http://ruv.localhost")
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
