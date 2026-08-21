using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Components;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class EditingAndFocus : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> ThreeEpisodes =
    [
        new(TvdbId: 100, Name: "Pilot", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 101, Name: "Second", SeasonNumber: 1, EpisodeNumber: 2),
        new(TvdbId: 102, Name: "Third", SeasonNumber: 1, EpisodeNumber: 3),
    ];

    private readonly IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>> _episodesHandler;

    public EditingAndFocus()
    {
        _episodesHandler = Substitute.For<IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>>();
        _episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(ThreeEpisodes);

        IRequestHandler<MatchEpisodeCommand> matchHandler = Substitute.For<IRequestHandler<MatchEpisodeCommand>>();
        matchHandler
            .Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);

        Services.AddTransient(_ => _episodesHandler);
        Services.AddTransient(_ => matchHandler);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenEditingExistingMatches_EachComboboxPrefillsWithMatchLabel()
    {
        // Arrange
        List<TvdbEpisodeSummary> currentMatches =
        [
            new(100, 1, 1, false, false, null),
            new(101, 1, 2, false, false, null),
        ];

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();

        // Act
        await cut.Instance.OpenAsync("ruv-1", currentMatches: currentMatches, tvdbSeriesId: 42, episodeTitle: string.Empty, siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 2);

        // Assert — each input shows the label for its matched episode
        IReadOnlyList<IElement> inputs = cut.FindAll("input[role=combobox]");
        string firstValue = inputs[0].GetAttribute("value").ShouldNotBeNull();
        string secondValue = inputs[1].GetAttribute("value").ShouldNotBeNull();
        firstValue.ShouldContain("S01E01");
        secondValue.ShouldContain("S01E02");
    }

    [Fact]
    public async Task WhenAddAnotherMatchClicked_PrefillsNextEpisodeInFullList()
    {
        // Arrange — open with episode 100 (first) auto-selected via title parsing
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: "þáttur 1", siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 1);

        // Act — clicking Add another match should prefill next episode (101)
        await cut.Find(".match-add-button").ClickAsync(new MouseEventArgs());
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 2);

        // Assert — second entry prefilled with S01E02 label
        IReadOnlyList<IElement> inputs = cut.FindAll("input[role=combobox]");
        string secondValue = inputs[1].GetAttribute("value").ShouldNotBeNull();
        secondValue.ShouldContain("S01E02");
    }

    [Fact]
    public async Task WhenNoEpisodesForSeries_ShowsEmptySeriesMessage()
    {
        // Arrange
        _episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns([]);

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();

        // Act
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: string.Empty, siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll(".dialog-spinner-container").Count == 0);

        // Assert
        cut.Markup.ShouldContain("No episodes found for this series.");
        cut.FindAll("input[role=combobox]").ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenEpisodeLoadFails_RendersErrorAndNoCombobox()
    {
        // Arrange
        _episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Network error"));

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();

        // Act
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: string.Empty, siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll(".dialog-error").Count > 0);

        // Assert
        cut.Find(".dialog-error").TextContent.ShouldContain("Failed to load episodes");
        cut.FindAll("input[role=combobox]").ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenRemovingThirdEntry_FocusMovesToPreviousCombobox()
    {
        // Arrange — open with three existing matches so removing one leaves 2 (> 1)
        List<TvdbEpisodeSummary> currentMatches =
        [
            new(100, 1, 1, false, false, null),
            new(101, 1, 2, false, false, null),
            new(102, 1, 3, false, false, null),
        ];

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: currentMatches, tvdbSeriesId: 42, episodeTitle: string.Empty, siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 3);

        // Act — remove the third entry (index 2); 2 remain so focus goes to previous combobox
        IReadOnlyList<IElement> removeButtons = cut.FindAll(".match-entry-remove");
        await removeButtons[2].ClickAsync(new MouseEventArgs());

        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 2);

        // Assert — two comboboxes remain; FocusAndSelectAsync was called on previous combobox
        // selectElementText is called twice: once on dialog open (first entry autofocus), once on remove
        cut.FindAll("input[role=combobox]").Count.ShouldBe(2);
        JSInterop.VerifyInvoke("selectElementText", calledTimes: 2);
    }

    [Fact]
    public async Task WhenReducingToOneEntry_FocusMovesToAddButton()
    {
        // Arrange — open with two existing matches
        List<TvdbEpisodeSummary> currentMatches =
        [
            new(100, 1, 1, false, false, null),
            new(101, 1, 2, false, false, null),
        ];

        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: currentMatches, tvdbSeriesId: 42, episodeTitle: string.Empty, siblingEpisodes: []);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 2);

        // Act — remove one entry, leaving one remaining; focus goes to add button
        IElement removeButton = cut.Find(".match-entry-remove");
        await removeButton.ClickAsync(new MouseEventArgs());
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 1);

        // Assert — focusElement invoked for the add button
        JSInterop.VerifyInvoke("focusElement");
    }
}
