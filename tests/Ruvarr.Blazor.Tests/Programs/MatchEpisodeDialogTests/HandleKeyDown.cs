using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.MatchEpisode;

namespace Ruvarr.Blazor.Tests.Programs.MatchEpisodeDialogTests;

public sealed class HandleKeyDown : BunitContext
{
    private readonly IRequestHandler<MatchEpisodeCommand> _handler;

    public HandleKeyDown()
    {
        _handler = Substitute.For<IRequestHandler<MatchEpisodeCommand>>();
        _handler
            .Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddTransient(_ => _handler);
        Services.AddSingleton(Substitute.For<IJSRuntime>());
    }

    [Fact]
    public async Task EnterKey_WhenMatchEnabled_InvokesSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "99999" });

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        await _handler.Received(1).Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
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
        await _handler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
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
        await _handler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoRapidEnterKeys_OnlyInvokeSubmitMatchOnce()
    {
        // Arrange
        TaskCompletionSource<RuvarrResult> tcs = new();
        _handler
            .Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentTvdbId: null);
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "99999" });

        // Act — fire two Enter presses before the first handler call completes
        Task first = cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        Task second = cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        tcs.SetResult(RuvarrResult.Success);
        await Task.WhenAll(first, second);

        // Assert
        await _handler.Received(1).Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
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
        await _handler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }
}
