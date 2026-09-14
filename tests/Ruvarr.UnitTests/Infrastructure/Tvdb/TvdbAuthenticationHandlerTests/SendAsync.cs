using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Tvdb.TvdbAuthenticationHandlerTests;

public sealed class SendAsync
{
    private const string ApiKey = "test-api-key";
    private const string AccessToken = "fake-access-token";
    private const string LoginResponseBody = """{"data":{"token":"fake-access-token"},"status":"success"}""";

    // TDD cycle (a): handler attaches Bearer token from login to outgoing request
    [Fact]
    public async Task WhenLoginSucceeds_AttachesBearerTokenToRequest()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

        // CA2000: handler disposed by test via using
#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        AuthenticationHeaderValue? capturedAuth = null;
        sut.InnerHandler = new CountingLoginHandler(
            loginResponse: LoginResponseBody,
            onApplicationRequest: request => { capturedAuth = request.Headers.Authorization; });

        // CA2000: HttpClient does not own the handler (disposeHandler: false)
#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act
        await httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken);

        // Assert
        capturedAuth.ShouldNotBeNull();
        capturedAuth.Scheme.ShouldBe("Bearer");
        capturedAuth.Parameter.ShouldBe(AccessToken);
    }

    // TDD cycle (b): second request uses cached token — no additional login
    [Fact]
    public async Task WhenTokenIsCached_DoesNotLoginAgain()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey + "-b"));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        CountingLoginHandler inner = new(loginResponse: LoginResponseBody);
        sut.InnerHandler = inner;

#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act
        await httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken);
        await httpClient.GetAsync("v4/series/2", TestContext.Current.CancellationToken);

        // Assert
        inner.LoginCount.ShouldBe(1);
    }

    // TDD cycle (c): re-logs in when API key changes
    [Fact]
    public async Task WhenApiKeyChanges_ReLoginsAndUsesNewToken()
    {
        // Arrange
        const string NewToken = "new-token";

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey + "-c"));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        string secondLoginJson = $$"""{"data":{"token":"{{NewToken}}"},"status":"success"}""";
        AuthenticationHeaderValue? secondRequestAuth = null;
        int applicationRequestCount = 0;

        sut.InnerHandler = new CountingLoginHandler(
            loginResponse: LoginResponseBody,
            secondLoginResponse: secondLoginJson,
            onApplicationRequest: request =>
            {
                applicationRequestCount++;
                if (applicationRequestCount == 2)
                {
                    secondRequestAuth = request.Headers.Authorization;
                }
            });

#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act — first request with old key
        await httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken);

        // Change the API key
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: "new-api-key"));

        // Second request with new key — must re-login
        await httpClient.GetAsync("v4/series/2", TestContext.Current.CancellationToken);

        // Assert
        secondRequestAuth.ShouldNotBeNull();
        secondRequestAuth.Parameter.ShouldBe(NewToken);
    }

    // TDD cycle (d): throws when login response token is empty/whitespace
    [Fact]
    public async Task WhenLoginResponseTokenIsEmpty_ThrowsInvalidOperationException()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey + "-d"));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        sut.InnerHandler = new CountingLoginHandler(
            loginResponse: """{"data":{"token":""},"status":"success"}""");

#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act / Assert
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken));
        ex.Message.ShouldBe("Failed to authenticate with the TVDB");
    }

    // TDD cycle (e): 401 from downstream triggers re-login and retry with fresh token
    [Fact]
    public async Task WhenDownstream401_ReLoginsAndRetriesWithFreshToken()
    {
        // Arrange
        const string FreshToken = "fresh-token-after-relogin";
        const string FreshLoginJson = """{"data":{"token":"fresh-token-after-relogin"},"status":"success"}""";

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey + "-e"));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

        // CA2000: handler disposed by test via using
#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        AuthenticationHeaderValue? retryRequestAuth = null;
        int applicationRequestCount = 0;

        CountingLoginHandler inner = new(
            loginResponse: LoginResponseBody,
            secondLoginResponse: FreshLoginJson,
            onApplicationRequest: request =>
            {
                applicationRequestCount++;
                // Capture auth only on the retry (second application request) — the
                // first request carries the cached token, the retry carries the fresh one.
                if (applicationRequestCount == 2)
                {
                    retryRequestAuth = request.Headers.Authorization;
                }
            },
            firstApplicationResponseStatus: HttpStatusCode.Unauthorized);
        sut.InnerHandler = inner;

        // CA2000: HttpClient does not own the handler (disposeHandler: false)
#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        applicationRequestCount.ShouldBe(2, "the request must be retried once");
        inner.LoginCount.ShouldBe(2, "one initial login plus one re-login after the 401");
        retryRequestAuth.ShouldNotBeNull();
        retryRequestAuth.Scheme.ShouldBe("Bearer");
        retryRequestAuth.Parameter.ShouldBe(FreshToken);
    }

    // TDD cycle (f): persistent 401 (re-login does not help) returns 401 without looping
    [Fact]
    public async Task WhenDownstream401PersistsAfterReLogin_ReturnsUnauthorizedWithoutRetryingAgain()
    {
        // Arrange
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(TvdbApiKey: ApiKey + "-f"));

        using MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 });

        // CA2000: handler disposed by test via using
#pragma warning disable CA2000
        using TvdbAuthenticationHandler sut = new(cache, settingsStore);
#pragma warning restore CA2000

        int applicationRequestCount = 0;

        sut.InnerHandler = new CountingLoginHandler(
            loginResponse: LoginResponseBody,
            onApplicationRequest: _ => { applicationRequestCount++; },
            firstApplicationResponseStatus: HttpStatusCode.Unauthorized,
            retryApplicationResponseStatus: HttpStatusCode.Unauthorized);

        // CA2000: HttpClient does not own the handler (disposeHandler: false)
#pragma warning disable CA2000
        using HttpClient httpClient = new(sut, disposeHandler: false)
        {
            BaseAddress = new Uri("https://tvdb.test/")
        };
#pragma warning restore CA2000

        // Act
        HttpResponseMessage response = await httpClient.GetAsync("v4/series/1", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        applicationRequestCount.ShouldBe(2, "must retry exactly once, no infinite loop");
    }

    /// <summary>
    /// Counts login requests and routes application requests to a capture callback.
    /// Login detection: POST to a path containing "login".
    /// </summary>
    private sealed class CountingLoginHandler : DelegatingHandler
    {
        private readonly string _loginResponse;
        private readonly string? _secondLoginResponse;
        private readonly Action<HttpRequestMessage>? _onApplicationRequest;
        private readonly HttpStatusCode _firstApplicationResponseStatus;
        private readonly HttpStatusCode _retryApplicationResponseStatus;
        private int _applicationRequestCount;

        internal int LoginCount { get; private set; }

        internal CountingLoginHandler(
            string loginResponse,
            string? secondLoginResponse = null,
            Action<HttpRequestMessage>? onApplicationRequest = null,
            HttpStatusCode firstApplicationResponseStatus = HttpStatusCode.OK,
            HttpStatusCode retryApplicationResponseStatus = HttpStatusCode.OK)
        {
            _loginResponse = loginResponse;
            _secondLoginResponse = secondLoginResponse;
            _onApplicationRequest = onApplicationRequest;
            _firstApplicationResponseStatus = firstApplicationResponseStatus;
            _retryApplicationResponseStatus = retryApplicationResponseStatus;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            bool isLogin = request.Method == HttpMethod.Post
                && (request.RequestUri?.AbsolutePath.Contains("login", StringComparison.Ordinal) ?? false);

            if (isLogin)
            {
                string response = LoginCount == 0 ? _loginResponse : (_secondLoginResponse ?? _loginResponse);
                LoginCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json")
                });
            }

            _onApplicationRequest?.Invoke(request);

            _applicationRequestCount++;
            HttpStatusCode status = _applicationRequestCount == 1
                ? _firstApplicationResponseStatus
                : _retryApplicationResponseStatus;

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
