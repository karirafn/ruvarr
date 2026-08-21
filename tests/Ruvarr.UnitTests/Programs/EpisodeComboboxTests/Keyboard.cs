using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class KeyboardInteraction : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> TestEpisodes =
    [
        new(TvdbId: 1, Name: "Pilot", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 2, Name: "Second", SeasonNumber: 1, EpisodeNumber: 2),
        new(TvdbId: 3, Name: "Third", SeasonNumber: 1, EpisodeNumber: 3),
    ];

    public KeyboardInteraction()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenArrowDown_MovesAriaActiveDescendantToFirstOption()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act — ArrowDown opens popup and highlights first
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert
        IElement input = cut.Find("input");
        string? activeDescendant = input.GetAttribute("aria-activedescendant");
        activeDescendant.ShouldNotBeNullOrWhiteSpace();

        // The referenced element should exist and be an option
        IElement highlighted = cut.Find($"#{activeDescendant}");
        highlighted.GetAttribute("role").ShouldBe("option");
        highlighted.ClassList.Contains("is-highlighted").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenArrowDownTwice_MovesHighlightToSecondOption()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert — second option highlighted
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options[0].ClassList.Contains("is-highlighted").ShouldBeFalse();
        options[1].ClassList.Contains("is-highlighted").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenArrowUp_OpenPopupHighlightingLastOption()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });

        // Assert — last option highlighted
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options[^1].ClassList.Contains("is-highlighted").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenArrowDown_InputRetainsFocus()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert — DOM focus stays on input (options do not receive focus)
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        foreach (IElement option in options)
        {
            option.GetAttribute("tabindex").ShouldBeNull();
        }

        // Popup is open but input element is still the interaction target
        cut.Find("input").GetAttribute("role").ShouldBe("combobox");
    }

    [Fact]
    public async Task WhenArrowNavigation_InvokesScrollOptionIntoView()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Assert
        JSInterop.VerifyInvoke("scrollOptionIntoView");
    }

    [Fact]
    public async Task WhenEnterPopupOpen_CommitsHighlightedAndClosesWithoutSubmit()
    {
        // Arrange
        bool submitRequested = false;
        int? capturedId = null;

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.OnSubmitRequested, EventCallback.Factory.Create(this, () => submitRequested = true))
            .Add(p => p.SelectedEpisodeIdChanged, EventCallback.Factory.Create<int?>(this, id => capturedId = id)));

        // Open popup and highlight first option
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });

        // Act — Enter with popup open
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        submitRequested.ShouldBeFalse();
        capturedId.ShouldBe(1);
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task WhenEnterPopupClosed_RaisesOnSubmitRequested()
    {
        // Arrange
        bool submitRequested = false;

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.OnSubmitRequested, EventCallback.Factory.Create(this, () => submitRequested = true)));

        // Popup is closed (initial state)
        // Act
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        submitRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenEscapePopupOpen_ClosesPopupAndRevertsQuery()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.SelectedEpisodeId, 1));

        // Open popup and type something
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Second" });

        // Act — Escape should close popup and revert query to selected label
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        IElement input = cut.Find("input");
        input.GetAttribute("aria-expanded").ShouldBe("false");
        // Query reverts to selected episode's label (TvdbId=1 → "S01E01 · Pilot")
        string? inputValue = input.GetAttribute("value");
        inputValue.ShouldNotBeNull();
        inputValue.ShouldContain("S01E01");
    }

    [Fact]
    public async Task WhenEscapePopupOpen_ClosesPopupInternally()
    {
        // Arrange — a JS keydown listener (registered via registerComboboxKeys) calls
        // event.preventDefault() selectively for ArrowUp/ArrowDown/Enter/Escape when
        // aria-expanded is "true", so the native <dialog> Escape-close is suppressed.
        // In bUnit we verify that the C# handler closes the popup correctly.
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Open the popup
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "S01" });
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("true");

        // Act — Escape while open
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert — popup closed (Escape handled internally, not bubbled)
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task WhenEscapePopupOpenWithNoSelection_RevertsQueryToEmpty()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Open popup and type something
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });

        // Act — Escape with no selection
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert — query reverts to empty (nothing selected)
        cut.Find("input").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenQueryChanges_AriaLiveTextUpdates()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });

        // Assert — aria-live region updated
        IElement liveRegion = cut.Find("[aria-live=polite]");
        liveRegion.TextContent.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task WhenNoMatchAndQueryChanges_AriaLiveTextAnnouncesNoMatch()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz" });

        // Assert
        IElement liveRegion = cut.Find("[aria-live=polite]");
        liveRegion.TextContent.ShouldContain("zzz");
    }

    [Fact]
    public async Task WhenEnterPopupOpenWithNoHighlight_ClosesPopupWithoutCommitting()
    {
        // Arrange — type a non-matching query so popup is open with no options and no highlight
        bool submitRequested = false;
        int? capturedId = null;

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.OnSubmitRequested, EventCallback.Factory.Create(this, () => submitRequested = true))
            .Add(p => p.SelectedEpisodeIdChanged, EventCallback.Factory.Create<int?>(this, id => capturedId = id)));

        // Open popup with no matches — _highlightedId will be null
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz" });
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("true");

        // Act — Enter with popup open but no highlighted option
        await cut.Find("input").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert — popup closes, no commit and no submit
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("false");
        capturedId.ShouldBeNull();
        submitRequested.ShouldBeFalse();
    }
}
