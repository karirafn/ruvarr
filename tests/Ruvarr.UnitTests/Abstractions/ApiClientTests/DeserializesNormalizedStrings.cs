using System.Net;
using System.Text;

using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.ApiClientTests;

public sealed class DeserializesNormalizedStrings
{
    // NFD: o + U+0308 (COMBINING DIAERESIS), a + U+0301 (COMBINING ACUTE ACCENT)
    // Written with explicit \u escapes so git/editor normalization cannot collapse them.
    private const string NfdTitle = "Skjaldb\u006f\u0308kustr\u0061\u0301kur";
    private const string NfcTitle = "Skjaldb\u00f6kustr\u00e1kur";

    [Fact]
    public async Task GetAsync_WhenResponseBodyContainsNfdString_DeserializesToNfc()
    {
        // Arrange
        string body = $"{{\"title\":\"{NfdTitle}\"}}";
        FakeLogger logger = new();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        TestableApiClient sut = new(logger, httpClient);

        // Act
        EpisodeResponse? result = await sut.TestGetAsync<EpisodeResponse>("/episodes/1", TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe(NfcTitle);
    }

    [Fact]
    public async Task PostAsync_WhenResponseBodyContainsNfdString_DeserializesToNfc()
    {
        // Arrange
        string body = $"{{\"title\":\"{NfdTitle}\"}}";
        FakeLogger logger = new();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        TestableApiClient sut = new(logger, httpClient);

        // Act
        EpisodeResponse? result = await sut.TestPostAsync<object, EpisodeResponse>("/api", new { }, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Title.ShouldBe(NfcTitle);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content) =>
        new(new FakeHttpMessageHandler(statusCode, content), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private sealed record EpisodeResponse(string Title);

    private sealed class TestableApiClient(ILogger logger, HttpClient httpClient)
        : ApiClient(logger, httpClient)
    {
        public Task<TResponse?> TestGetAsync<TResponse>(string path, CancellationToken cancellationToken) =>
            GetAsync<TResponse>(path, cancellationToken);

        public Task<TResponse?> TestPostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken) =>
            PostAsync<TRequest, TResponse>(path, body, cancellationToken);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private sealed class FakeLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
