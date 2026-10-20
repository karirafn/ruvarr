using System.Net;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging.Abstractions;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.ApiClientTests;

public sealed class GetAsyncResult
{
    private sealed record TestResponse(string Value);

    [Fact]
    public async Task GetAsync_When200_ReturnsSuccessResultWithDeserializedValue()
    {
        // Arrange
        string body = """{"value":"hello"}""";
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<TestResponse> result = await sut.TestGetAsync<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        TestResponse value = result.FromResult();
        value.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task GetAsync_When404_ReturnsNotFoundFailure()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.NotFound, "Not Found");
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<TestResponse> result = await sut.TestGetAsync<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApiClientErrors.NotFoundCode);
    }

    [Fact]
    public async Task GetAsync_When500_ReturnsRequestFailedFailure()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "Server Error");
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<TestResponse> result = await sut.TestGetAsync<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApiClientErrors.RequestFailedCode);
    }

    [Fact]
    public async Task GetAsync_WhenHttpRequestException_ReturnsRequestFailedFailure()
    {
        // Arrange
        using HttpClient httpClient = CreateThrowingHttpClient(new HttpRequestException("Network error"));
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<TestResponse> result = await sut.TestGetAsync<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApiClientErrors.RequestFailedCode);
    }

    [Fact]
    public async Task GetMany_WhenNullBody_ReturnsEmptyListSuccess()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, "null");
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<IReadOnlyList<TestResponse>> result = await sut.TestGetMany<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.FromResult().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMany_WhenGetAsyncFails_ForwardsFailureResult()
    {
        // Arrange
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "error");
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        Result<IReadOnlyList<TestResponse>> result = await sut.TestGetMany<TestResponse>("/test", TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ApiClientErrors.RequestFailedCode);
    }

    [Fact]
    public async Task WhenExplicitOptionsProvided_DeserializesUsingThoseOptions()
    {
        // Arrange — RuvJson.Default has RespectNullableAnnotations=true; a null value for a
        // non-nullable property throws JsonException. Under the lenient default it would not,
        // so this test fails if the explicit options are not threaded through to deserialization.
        string body = """{"value":null}""";
        using HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, body);
        TestableApiClient sut = new(NullLogger.Instance, httpClient);

        // Act
        JsonException ex = await Should.ThrowAsync<JsonException>(
            async () => await sut.TestGetAsync<TestResponse>("/test", RuvJson.Default, TestContext.Current.CancellationToken));

        // Assert
        ex.ShouldNotBeNull();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content) =>
        new(new StaticHandler(statusCode, content), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost")
        };

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "Handler is disposed by HttpClient via disposeHandler: true")]
    private static HttpClient CreateThrowingHttpClient(Exception exception) =>
        new(new ThrowingHandler(exception), disposeHandler: true)
        {
            BaseAddress = new Uri("http://localhost")
        };

    private sealed class TestableApiClient(ILogger logger, HttpClient httpClient)
        : ApiClient(logger, httpClient)
    {
        public Task<Result<TResponse>> TestGetAsync<TResponse>(string path, CancellationToken cancellationToken)
            where TResponse : notnull =>
            GetAsync<TResponse>(path, cancellationToken);

        public Task<Result<TResponse>> TestGetAsync<TResponse>(string path, JsonSerializerOptions options, CancellationToken cancellationToken)
            where TResponse : notnull =>
            GetAsync<TResponse>(path, options, cancellationToken);

        public Task<Result<IReadOnlyList<TResponse>>> TestGetMany<TResponse>(string path, CancellationToken cancellationToken)
            where TResponse : notnull =>
            GetMany<TResponse>(path, cancellationToken);
    }

    private sealed class StaticHandler(HttpStatusCode statusCode, string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }
}
