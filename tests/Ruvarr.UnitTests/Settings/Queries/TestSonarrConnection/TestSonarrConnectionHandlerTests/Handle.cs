using System.Net;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Settings;
using Ruvarr.Settings.Queries.TestSonarrConnection;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.Queries.TestSonarrConnection.TestSonarrConnectionHandlerTests;

public sealed class Handle
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();

    [Fact]
    public async Task ReturnsSuccessWhenSonarrRespondsWithOk()
    {
        // Arrange
        using HttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("http://localhost:8989", "test-key", UseStoredApiKey: false);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsFailureWhenSonarrRespondsWithError()
    {
        // Arrange
        using HttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized);
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("http://localhost:8989", "bad-key", UseStoredApiKey: false);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldContain("401");
    }

    [Fact]
    public async Task ReturnsFailureWhenConnectionFails()
    {
        // Arrange
        using HttpMessageHandler handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("http://localhost:8989", "test-key", UseStoredApiKey: false);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Description.ShouldContain("Connection refused");
    }

    [Fact]
    public async Task SubstitutesStoredApiKeyWhenUseStoredApiKeyIsTrue()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(SonarrApiKey: "real-secret-key"));
        using FakeHttpMessageHandler handler = new(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("http://localhost:8989", "****", UseStoredApiKey: true);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        handler.LastRequest.ShouldNotBeNull();
        handler.LastRequest.Headers.GetValues("X-Api-Key").ShouldBe(["real-secret-key"]);
    }

    [Fact]
    public async Task ReturnsFailureWhenBaseAddressIsNotAbsoluteUri()
    {
        // Arrange
        using HttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("not-a-valid-url", "test-key", UseStoredApiKey: false);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SettingsErrors.InvalidSonarrBaseAddressCode);
    }

    [Fact]
    public async Task ReturnsFailureWhenBaseAddressHasNonHttpScheme()
    {
        // Arrange
        using HttpMessageHandler handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        IHttpClientFactory factory = CreateFactory(handler);
        TestSonarrConnectionHandler sut = new(factory, _store);
        TestSonarrConnectionQuery query = new("ftp://localhost:8989", "test-key", UseStoredApiKey: false);

        // Act
        RuvarrResult result = await sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(SettingsErrors.InvalidSonarrBaseAddressCode);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "HttpClient is disposed by the handler under test")]
    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler)
    {
        IHttpClientFactory factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(new HttpClient(handler));
        return factory;
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly HttpRequestException? _exception;

        public HttpRequestMessage? LastRequest { get; private set; }

        public FakeHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public FakeHttpMessageHandler(HttpRequestException exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;

            if (_exception is not null)
            {
                throw _exception;
            }

            return Task.FromResult(new HttpResponseMessage(_statusCode!.Value));
        }
    }
}
