using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.ApiClientTests;

public sealed class TimeoutReturnsFailure
{
    [Fact]
    public async Task GetAsync_WhenHttpClientTimesOut_ReturnsRequestFailedResult()
    {
        // Arrange
        // The handler delays longer than the client timeout, so HttpClient throws TaskCanceledException
        // from its own internal timeout, even though the caller's CancellationToken is not cancelled.
        TimeSpan clientTimeout = TimeSpan.FromMilliseconds(50);
        TimeSpan handlerDelay = TimeSpan.FromSeconds(10);

        using DelayingHttpMessageHandler handler = new(handlerDelay);
        using HttpClient httpClient = new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost"),
            Timeout = clientTimeout
        };

        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        using CancellationTokenSource cts = new();
        CancellationToken callerToken = cts.Token;

        // Act
        Result<string> result = await sut.TestGetAsync<string>("/test", callerToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApiClientErrors.RequestFailedCode);
    }

    private sealed class TestableApiClient(ILogger logger, HttpClient httpClient)
        : ApiClient(logger, httpClient)
    {
        public Task<Result<TResponse>> TestGetAsync<TResponse>(string path, CancellationToken cancellationToken)
            where TResponse : notnull =>
            GetAsync<TResponse>(path, cancellationToken);
    }
}
