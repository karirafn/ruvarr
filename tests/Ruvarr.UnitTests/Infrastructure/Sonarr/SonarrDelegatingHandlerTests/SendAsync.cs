using NSubstitute;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr.SonarrDelegatingHandlerTests;

public sealed class SendAsync
{
    [Fact]
    public async Task RewritesPlaceholderBaseAddress_ToConfiguredSonarrAddress()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr:8989",
            SonarrApiKey: "test-api-key"));

        using SonarrDelegatingHandler sut = new(settingsStore);

        Uri? capturedUri = null;
        sut.InnerHandler = new CapturingHandler(request =>
        {
            capturedUri = request.RequestUri;
        });

        using HttpClient httpClient = new(sut, disposeHandler: false);
        httpClient.BaseAddress = new Uri("http://unconfigured");

        // Act
        await httpClient.GetAsync("api/v3/series", CancellationToken.None);

        // Assert
        capturedUri.ShouldNotBeNull();
        capturedUri.ToString().ShouldBe("http://sonarr:8989/api/v3/series");
    }

    [Fact]
    public async Task AddsApiKeyHeader_WhenConfigured()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr:8989",
            SonarrApiKey: "my-secret-key"));

        using SonarrDelegatingHandler sut = new(settingsStore);

        string? capturedApiKey = null;
        sut.InnerHandler = new CapturingHandler(request =>
        {
            capturedApiKey = request.Headers.GetValues("X-Api-Key").FirstOrDefault();
        });

        using HttpClient httpClient = new(sut, disposeHandler: false);
        httpClient.BaseAddress = new Uri("http://unconfigured");

        // Act
        await httpClient.GetAsync("api/v3/series", CancellationToken.None);

        // Assert
        capturedApiKey.ShouldBe("my-secret-key");
    }

    [Fact]
    public async Task PreservesPathAndQuery_WhenRewritingUri()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr:8989",
            SonarrApiKey: "key"));

        using SonarrDelegatingHandler sut = new(settingsStore);

        Uri? capturedUri = null;
        sut.InnerHandler = new CapturingHandler(request =>
        {
            capturedUri = request.RequestUri;
        });

        using HttpClient httpClient = new(sut, disposeHandler: false);
        httpClient.BaseAddress = new Uri("http://unconfigured");

        // Act
        await httpClient.GetAsync("api/v3/series?tvdbId=12345", CancellationToken.None);

        // Assert
        capturedUri.ShouldNotBeNull();
        capturedUri.ToString().ShouldBe("http://sonarr:8989/api/v3/series?tvdbId=12345");
    }

    private sealed class CapturingHandler(Action<HttpRequestMessage> onSend) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            onSend(request);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
