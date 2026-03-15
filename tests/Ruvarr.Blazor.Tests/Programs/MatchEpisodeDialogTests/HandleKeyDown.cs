using System.Net;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using NSubstitute;

using Ruvarr.Blazor.Programs;

using Shouldly;

namespace Ruvarr.Blazor.Tests.Programs.MatchEpisodeDialogTests;

public sealed class HandleKeyDown : BunitContext
{
    private readonly MockHttpMessageHandler _httpHandler = new();
    private readonly HttpClient _httpClient;

    public HandleKeyDown()
    {
        _httpClient = new HttpClient(_httpHandler) { BaseAddress = new Uri("http://localhost") };
        Services.AddSingleton(new RuvApiClient(_httpClient));
        Services.AddSingleton(Substitute.For<IJSRuntime>());
    }

    [Fact]
    public async Task EnterKey_WhenMatchEnabled_InvokesSubmitMatch()
    {
        // Arrange
        _httpHandler.RespondWith(HttpStatusCode.OK);
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "99999" });

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        _httpHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task EnterKey_WhenInputIsEmpty_DoesNotInvokeSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        _httpHandler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task EnterKey_WhenInputUnchangedFromCurrentMatch_DoesNotInvokeSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: 12345);

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        _httpHandler.RequestCount.ShouldBe(0);
    }

    [Fact]
    public async Task TwoRapidEnterKeys_OnlyInvokeSubmitMatchOnce()
    {
        // Arrange
        TaskCompletionSource<HttpResponseMessage> tcs = new();
        _httpHandler.RespondWith(tcs.Task);
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "99999" });

        // Act — fire two Enter presses before the first HTTP response completes
        Task first = cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Task second = cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        using HttpResponseMessage pendingResponse = new(HttpStatusCode.OK);
        tcs.SetResult(pendingResponse);
        await Task.WhenAll(first, second);

        // Assert
        _httpHandler.RequestCount.ShouldBe(1);
    }

    [Fact]
    public async Task OtherKey_DoesNotInvokeSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "99999" });

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        // Assert
        _httpHandler.RequestCount.ShouldBe(0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient.Dispose();
            _httpHandler.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private Task<HttpResponseMessage> _response = Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    public int RequestCount { get; private set; }

    public void RespondWith(HttpStatusCode statusCode) =>
        _response = Task.FromResult(new HttpResponseMessage(statusCode));

    public void RespondWith(Task<HttpResponseMessage> response) => _response = response;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        return await _response;
    }
}
