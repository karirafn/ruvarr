using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;

using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class EpisodeFiltering : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> TestEpisodes =
    [
        new(TvdbId: 1, Name: "Pilot", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 2, Name: "Second", SeasonNumber: 1, EpisodeNumber: 2),
        new(TvdbId: 3, Name: "Premiere", SeasonNumber: 2, EpisodeNumber: 1),
        new(TvdbId: 4, Name: "Finale", SeasonNumber: 2, EpisodeNumber: 2),
    ];

    public EpisodeFiltering()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenTypingQuery_RendersMatchingOptionsAcrossSeasons()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "S01" });

        // Assert
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options.Count.ShouldBe(2);
        options[0].TextContent.Trim().ShouldContain("S01E01");
        options[1].TextContent.Trim().ShouldContain("S01E02");
    }

    [Fact]
    public async Task WhenTypingMatchesAcrossSeasons_RendersAllMatchingOptions()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act — "E01" matches S01E01 and S02E01
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "E01" });

        // Assert
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options.Count.ShouldBe(2);
        options[0].TextContent.Trim().ShouldContain("S01E01");
        options[1].TextContent.Trim().ShouldContain("S02E01");
    }

    [Fact]
    public async Task WhenMoreThan100Matches_RendersCapLineWithTrueTotal()
    {
        // Arrange
        IReadOnlyList<TvdbSeriesEpisode> manyEpisodes = [.. Enumerable
            .Range(1, 120)
            .Select(i => new TvdbSeriesEpisode(TvdbId: i, Name: "ep", SeasonNumber: 1, EpisodeNumber: i))];

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, manyEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act — type something that matches all 120
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "ep" });

        // Assert
        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options.Count.ShouldBe(100);

        IElement capLine = cut.Find(".episode-combobox-cap");
        capLine.TextContent.ShouldContain("100");
        capLine.TextContent.ShouldContain("120");
    }

    [Fact]
    public async Task WhenNoMatch_RendersEmptyStateWithQuery()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "zzz" });

        // Assert
        IElement empty = cut.Find(".episode-combobox-empty");
        empty.TextContent.ShouldContain("zzz");
        cut.FindAll("[role=option]").Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhenOptionChosen_SetsSelectedEpisodeIdFromTvdbId()
    {
        // Arrange
        int? capturedId = null;

        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.SelectedEpisodeIdChanged, EventCallback.Factory.Create<int?>(this, id => capturedId = id)));

        // Open popup first
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "Pilot" });

        // Act — mousedown on first option (TvdbId = 1)
        await cut.FindAll("[role=option]")[0].MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        capturedId.ShouldBe(1);
    }

    [Fact]
    public void RendersWithCorrectAriaRoles()
    {
        // Arrange & Act
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Assert — static ARIA attributes
        IElement input = cut.Find("input");
        input.GetAttribute("role").ShouldBe("combobox");
        input.GetAttribute("aria-autocomplete").ShouldBe("list");
        input.GetAttribute("aria-haspopup").ShouldBe("listbox");
        input.GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task WhenPopupOpen_RendersListboxWithOptionRoles()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act — open popup
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "S01" });

        // Assert
        IElement listbox = cut.Find("[role=listbox]");
        listbox.ShouldNotBeNull();

        IReadOnlyList<IElement> options = cut.FindAll("[role=option]");
        options.ShouldNotBeEmpty();

        string? ariaControls = cut.Find("input").GetAttribute("aria-controls");
        ariaControls.ShouldNotBeNullOrWhiteSpace();
        cut.Find($"#{ariaControls}").ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenOptionSelected_RendersAriaSelectedTrue()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1)
            .Add(p => p.SelectedEpisodeId, 1));

        // Act — open popup
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "S01" });

        // Assert — episode with TvdbId=1 has aria-selected=true
        IElement selectedOption = cut.FindAll("[role=option]")
            .First(o => o.GetAttribute("aria-selected") == "true");
        selectedOption.TextContent.Trim().ShouldContain("S01E01");
    }
}
