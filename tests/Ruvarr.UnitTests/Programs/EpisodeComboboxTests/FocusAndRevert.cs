using Bunit;

using Microsoft.AspNetCore.Components;

using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class FocusAndRevert : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> TestEpisodes =
    [
        new(TvdbId: 1, Name: "Pilot", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 2, Name: "Second", SeasonNumber: 1, EpisodeNumber: 2),
    ];

    public FocusAndRevert()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenBlurWithNoChoiceMade_RevertsQueryToSelectedLabel()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.SelectedEpisodeId, 1));

        // Open popup and type something different
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Second" });

        // Act — blur without choosing
        await cut.Find("input").BlurAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        // Assert — query reverts to selected episode label
        string? value = cut.Find("input").GetAttribute("value");
        value.ShouldNotBeNull();
        value.ShouldContain("S01E01");
        value.ShouldContain("Pilot");
    }

    [Fact]
    public async Task WhenBlurWithNoChoiceAndNoSelection_RevertsQueryToEmpty()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Open popup and type something
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });

        // Act — blur without choosing
        await cut.Find("input").BlurAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        // Assert — query reverts to empty (no selection)
        cut.Find("input").GetAttribute("value").ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task WhenBlurWithNoChoiceMade_ClosesPopup()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Open popup
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("true");

        // Act
        await cut.Find("input").BlurAsync(new Microsoft.AspNetCore.Components.Web.FocusEventArgs());

        // Assert
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task WhenFocusAndSelectAsyncCalled_InvokesSelectElementText()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Instance.FocusAndSelectAsync();

        // Assert
        JSInterop.VerifyInvoke("selectElementText");
    }

    [Fact]
    public async Task WhenOptionCommittedViaMousedown_ClosesPopupAndSetsValue()
    {
        // Arrange
        int? capturedId = null;

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.SelectedEpisodeIdChanged, EventCallback.Factory.Create<int?>(this, id => capturedId = id)));

        // Open popup
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });

        // Act — mousedown fires before blur; commit the selection
        await cut.FindAll("[role=option]")[0].MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        capturedId.ShouldBe(1);
        cut.Find("input").GetAttribute("aria-expanded").ShouldBe("false");
        string? value = cut.Find("input").GetAttribute("value");
        value.ShouldNotBeNull();
        value.ShouldContain("S01E01");
    }
}
