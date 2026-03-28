using System.Net;

using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.ApiClientTests;

public sealed class TruncateResponseContent
{
    private const int MaxContentLength = 512;

    [Fact]
    public async Task GetAsync_TruncatesResponseContentWhenLongerThanLimit()
    {
        // Arrange
        string longContent = new('x', MaxContentLength + 100);
        string expectedTruncated = new('x', MaxContentLength);

        FakeLogger logger = new();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.BadRequest, longContent);
        TestableApiClient sut = new(logger, httpClient);

        // Act
        await sut.TestGetAsync<string>("/test", TestContext.Current.CancellationToken);

        // Assert
        logger.LastLoggedContent.ShouldBe(expectedTruncated);
    }

    [Fact]
    public async Task GetAsync_DoesNotTruncateResponseContentWhenAtLimit()
    {
        // Arrange
        string content = new('y', MaxContentLength);

        FakeLogger logger = new();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.BadRequest, content);
        TestableApiClient sut = new(logger, httpClient);

        // Act
        await sut.TestGetAsync<string>("/test", TestContext.Current.CancellationToken);

        // Assert
        logger.LastLoggedContent.ShouldBe(content);
    }

    [Fact]
    public async Task PostAsync_TruncatesResponseContentWhenLongerThanLimit()
    {
        // Arrange
        string longContent = new('z', MaxContentLength + 50);
        string expectedTruncated = new('z', MaxContentLength);

        FakeLogger logger = new();
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, longContent);
        TestableApiClient sut = new(logger, httpClient);

        // Act
        await sut.TestPostAsync("/test", "body", TestContext.Current.CancellationToken);

        // Assert
        logger.LastLoggedContent.ShouldBe(expectedTruncated);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content) =>
        new(new FakeHttpMessageHandler(statusCode, content), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private sealed class TestableApiClient(ILogger logger, HttpClient httpClient)
        : ApiClient(logger, httpClient)
    {
        public Task<TResponse?> TestGetAsync<TResponse>(string path, CancellationToken cancellationToken) =>
            GetAsync<TResponse>(path, cancellationToken);

        public Task TestPostAsync<T>(string path, T body, CancellationToken cancellationToken) =>
            PostAsync(path, body, cancellationToken);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    private sealed class FakeLogger : ILogger
    {
        public string? LastLoggedContent { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IReadOnlyList<KeyValuePair<string, object?>> properties)
            {
                foreach (KeyValuePair<string, object?> kvp in properties)
                {
                    if (kvp.Key == "Content")
                    {
                        LastLoggedContent = kvp.Value?.ToString();
                    }
                }
            }
        }
    }
}
