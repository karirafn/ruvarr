using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.ClearManualSeasons;
using Ruvarr.Programs.Commands.SetManualSeasons;
using Ruvarr.Programs.Components;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.SeasonSelectorTests;

public sealed class DialogBehaviour : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> ThreeSeasons =
    [
        new(TvdbId: 101, Name: "A", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 102, Name: "B", SeasonNumber: 1, EpisodeNumber: 2),
        new(TvdbId: 201, Name: "C", SeasonNumber: 2, EpisodeNumber: 1),
        new(TvdbId: 301, Name: "D", SeasonNumber: 3, EpisodeNumber: 1),
    ];

    private readonly IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>> _episodes =
        Substitute.For<IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>>();
    private readonly IRequestHandler<SetManualSeasonsCommand> _set = Substitute.For<IRequestHandler<SetManualSeasonsCommand>>();
    private readonly IRequestHandler<ClearManualSeasonsCommand> _clear = Substitute.For<IRequestHandler<ClearManualSeasonsCommand>>();
    private static readonly int[] SeasonsOneAndThree = [1, 3];

    private int _savedCount;

    public DialogBehaviour()
    {
        _episodes.Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>()).Returns(ThreeSeasons);
        _set.Handle(Arg.Any<SetManualSeasonsCommand>(), Arg.Any<CancellationToken>()).Returns(RuvarrResult.Success);
        _clear.Handle(Arg.Any<ClearManualSeasonsCommand>(), Arg.Any<CancellationToken>()).Returns(RuvarrResult.Success);
        Services.AddTransient(_ => _episodes);
        Services.AddTransient(_ => _set);
        Services.AddTransient(_ => _clear);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private IRenderedComponent<SeasonSelector> RenderSelector(IReadOnlyList<int> manualSeasons, string? token = "II", int? number = 2) =>
        Render<SeasonSelector>(p => p
            .Add(x => x.ProgramRuvId, 7)
            .Add(x => x.TvdbSeriesId, 1000)
            .Add(x => x.ManualSeasons, manualSeasons)
            .Add(x => x.ParsedSeasonToken, token)
            .Add(x => x.ParsedSeasonNumber, number)
            .Add(x => x.OnSaved, () => _savedCount++));

    private static async Task OpenAsync(IRenderedComponent<SeasonSelector> cut)
    {
        await cut.Find(".season-selector-trigger").ClickAsync(new MouseEventArgs());
        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Loading seasons']").Count == 0);
    }

    private static IElement Row(IRenderedComponent<SeasonSelector> cut, string label) =>
        cut.FindAll("dialog label").Single(x => x.TextContent.Trim().StartsWith(label, StringComparison.Ordinal));

    private static IElement Checkbox(IRenderedComponent<SeasonSelector> cut, string label) =>
        Row(cut, label).QuerySelector("input[type=checkbox]")!;

    private static IElement SaveButton(IRenderedComponent<SeasonSelector> cut) => cut.Find(".dialog-button--primary");

    [Fact]
    public void Trigger_ShowsTheCollapsedValueAndIsActiveForManualSeasons()
    {
        // Act
        IRenderedComponent<SeasonSelector> cut = RenderSelector([1, 2, 3, 4, 5]);

        // Assert
        IElement trigger = cut.Find(".season-selector-trigger");
        trigger.TextContent.Trim().ShouldBe("Season: 1, 2, 3 +2");
        trigger.GetAttribute("aria-label").ShouldBe("Season: 1, 2, 3, 4, 5");
        trigger.GetAttribute("aria-haspopup").ShouldBe("dialog");
        trigger.ClassList.ShouldContain("action-button--active");
    }

    [Fact]
    public void Trigger_IsNotActiveWhenAutomatic()
    {
        // Act
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);

        // Assert
        IElement trigger = cut.Find(".season-selector-trigger");
        trigger.TextContent.Trim().ShouldBe("Season: Auto (2)");
        trigger.ClassList.ShouldNotContain("action-button--active");
    }

    [Fact]
    public async Task Open_ListsTheSeriesRegularSeasonsWithTheSavedOnesChecked()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([2]);

        // Act
        await OpenAsync(cut);

        // Assert
        Checkbox(cut, "Automatic (II → 2)").IsChecked().ShouldBeFalse();
        Checkbox(cut, "Season 1").IsChecked().ShouldBeFalse();
        Checkbox(cut, "Season 2").IsChecked().ShouldBeTrue();
        Checkbox(cut, "Season 3").IsChecked().ShouldBeFalse();
        SaveButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Open_KeepsASavedSeasonTvdbNoLongerLists()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([4]);

        // Act
        await OpenAsync(cut);

        // Assert
        Row(cut, "Season 4").TextContent.ShouldContain("(not on TVDB)");
        Checkbox(cut, "Season 4").IsChecked().ShouldBeTrue();
    }

    [Fact]
    public async Task CheckingASeason_UnchecksAutomaticAndAnnouncesIt()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);
        await OpenAsync(cut);

        // Act
        await Checkbox(cut, "Season 1").ChangeAsync(new ChangeEventArgs { Value = true });

        // Assert
        Checkbox(cut, "Automatic").IsChecked().ShouldBeFalse();
        cut.Find("[aria-live=polite]").TextContent.ShouldBe("Automatic turned off");
        SaveButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Save_SetsTheStagedSeasonsAndNotifiesTheParent()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);
        await OpenAsync(cut);
        await Checkbox(cut, "Season 3").ChangeAsync(new ChangeEventArgs { Value = true });
        await Checkbox(cut, "Season 1").ChangeAsync(new ChangeEventArgs { Value = true });

        // Act
        await SaveButton(cut).ClickAsync(new MouseEventArgs());

        // Assert
        await _set.Received(1).Handle(
            Arg.Is<SetManualSeasonsCommand>(c => c.RuvId == 7 && c.Seasons.SequenceEqual(SeasonsOneAndThree)),
            Arg.Any<CancellationToken>());
        _savedCount.ShouldBe(1);
        JSInterop.VerifyInvoke("closeDialog");
    }

    [Fact]
    public async Task Save_ClearsWhenSwitchedBackToAutomatic()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([2]);
        await OpenAsync(cut);
        await Checkbox(cut, "Automatic").ChangeAsync(new ChangeEventArgs { Value = true });

        // Act
        await SaveButton(cut).ClickAsync(new MouseEventArgs());

        // Assert
        await _clear.Received(1).Handle(Arg.Is<ClearManualSeasonsCommand>(c => c.RuvId == 7), Arg.Any<CancellationToken>());
        _savedCount.ShouldBe(1);
    }

    [Fact]
    public async Task Save_RejectedByTheServer_KeepsTheDialogOpenWithTheMessage()
    {
        // Arrange
        _set.Handle(Arg.Any<SetManualSeasonsCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(ProgramErrors.ManualSeasonNotInSeries));
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);
        await OpenAsync(cut);
        await Checkbox(cut, "Season 2").ChangeAsync(new ChangeEventArgs { Value = true });

        // Act
        await SaveButton(cut).ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find("dialog [role=alert]").TextContent.ShouldBe(ProgramErrors.ManualSeasonNotInSeries.Description);
        Checkbox(cut, "Season 2").IsChecked().ShouldBeTrue();
        _savedCount.ShouldBe(0);
        JSInterop.Invocations.ShouldNotContain(x => x.Identifier == "closeDialog");
    }

    [Fact]
    public async Task LoadError_ShowsRetryAndKeepsAutomaticAvailable()
    {
        // Arrange
        _episodes.Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("down"));
        IRenderedComponent<SeasonSelector> cut = RenderSelector([2]);

        // Act
        await OpenAsync(cut);

        // Assert
        cut.Find("dialog [role=alert]").TextContent.ShouldContain("Couldn't load seasons from TVDB.");
        cut.FindAll("button").ShouldContain(x => x.TextContent.Trim() == "Retry");
        await Checkbox(cut, "Automatic").ChangeAsync(new ChangeEventArgs { Value = true });
        SaveButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Empty_OffersOnlyAutomatic()
    {
        // Arrange
        _episodes.Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<TvdbSeriesEpisode>());
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);

        // Act
        await OpenAsync(cut);

        // Assert
        cut.Markup.ShouldContain("TVDB lists no regular seasons for this series.");
        cut.FindAll("dialog input[type=checkbox]").Count.ShouldBe(1);
    }

    [Fact]
    public async Task SeasonsAreLoadedOncePerSeries()
    {
        // Arrange
        IRenderedComponent<SeasonSelector> cut = RenderSelector([]);
        await OpenAsync(cut);
        await cut.Find("dialog").TriggerEventAsync("onclose", EventArgs.Empty);

        // Act
        await OpenAsync(cut);

        // Assert
        await _episodes.Received(1).Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>());
    }
}
