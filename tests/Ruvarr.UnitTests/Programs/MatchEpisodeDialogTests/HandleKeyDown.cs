using Bunit;

using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Components;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class HandleKeyDown : BunitContext
{
    private readonly IRequestHandler<MatchEpisodeCommand> _matchHandler;
    private readonly IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>> _episodesHandler;

    public HandleKeyDown()
    {
        _matchHandler = Substitute.For<IRequestHandler<MatchEpisodeCommand>>();
        _matchHandler
            .Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);

        _episodesHandler = Substitute.For<IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>>();
        _episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns([new TvdbSeriesEpisode(99999, "Test Episode", 1, 1)]);

        Services.AddTransient(_ => _matchHandler);
        Services.AddTransient(_ => _episodesHandler);
        Services.AddSingleton(Substitute.For<IJSRuntime>());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenMatchEnabled_EnterKeyInvokesSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: "þáttur 1", siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count > 0);

        // Season auto-selected, episode auto-selected via title parsing — combobox popup is closed
        // Act — Enter with popup closed triggers submit shortcut
        await cut.Find("input[role=combobox]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        await _matchHandler.Received(1).Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNoEpisodeSelected_EnterKeyDoesNotInvokeSubmitMatch()
    {
        // Arrange
        _episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns([new TvdbSeriesEpisode(99999, "Test Episode", 1, 1)]);

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: "Mynd", siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count > 0);

        // Episode not auto-selected because title doesn't match "þáttur N" pattern
        // Act — Enter with popup closed triggers submit shortcut, but match is disabled so no submit
        await cut.Find("input[role=combobox]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        await _matchHandler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSelectedMatchesCurrentMatches_EnterKeyDoesNotInvokeSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        List<TvdbEpisodeSummary> currentMatches = [new(99999, 1, 1, false, false, null)];
        await cut.Instance.OpenAsync("ruv-1", currentMatches: currentMatches, tvdbSeriesId: 42, episodeTitle: "þáttur 1", siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count > 0);

        // Episode auto-selected to 99999 which equals current match
        // Act — Enter with popup closed triggers submit shortcut, but match is disabled (same as current)
        await cut.Find("input[role=combobox]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        await _matchHandler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNonEnterKeyPressed_DoesNotInvokeSubmitMatch()
    {
        // Arrange
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: "þáttur 1", siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count > 0);

        // Act — a non-Enter key does not submit
        await cut.Find("input[role=combobox]").KeyDownAsync(new KeyboardEventArgs { Key = "a" });

        // Assert
        await _matchHandler.DidNotReceive().Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>());
    }
}
