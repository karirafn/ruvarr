using AngleSharp.Dom;

using Bunit;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Components;
using Ruvarr.Programs.Queries.GetEpisodeMatchSuggestion;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class Suggestion : BunitContext
{
    private const string NoteId = "match-suggestion-note";

    private static readonly IReadOnlyList<TvdbSeriesEpisode> Episodes =
    [
        new(TvdbId: 501, Name: "Upphaf", SeasonNumber: 5, EpisodeNumber: 1),
        new(TvdbId: 304, Name: "Heimkoman", SeasonNumber: 3, EpisodeNumber: 4),
        new(TvdbId: 204, Name: "Fjórði", SeasonNumber: 2, EpisodeNumber: 4),
    ];

    private readonly IRequestHandler<GetEpisodeMatchSuggestionQuery, EpisodeMatchSuggestion?> _suggestionHandler;
    private readonly IRequestHandler<MatchEpisodeCommand> _matchHandler;

    public Suggestion()
    {
        IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>> episodesHandler =
            Substitute.For<IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>>();
        episodesHandler
            .Handle(Arg.Any<GetTvdbSeriesEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Episodes);

        _suggestionHandler = Substitute.For<IRequestHandler<GetEpisodeMatchSuggestionQuery, EpisodeMatchSuggestion?>>();

        _matchHandler = Substitute.For<IRequestHandler<MatchEpisodeCommand>>();
        _matchHandler
            .Handle(Arg.Any<MatchEpisodeCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);

        Services.AddTransient(_ => episodesHandler);
        Services.AddTransient(_ => _suggestionHandler);
        Services.AddTransient(_ => _matchHandler);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void GivenSuggestion(EpisodeMatchSuggestion? suggestion) =>
        _suggestionHandler
            .Handle(Arg.Any<GetEpisodeMatchSuggestionQuery>(), Arg.Any<CancellationToken>())
            .Returns(suggestion);

    private async Task<IRenderedComponent<MatchEpisodeDialog>> OpenUnmatchedAsync(string episodeTitle = "Heimkoman")
    {
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [], tvdbSeriesId: 42, episodeTitle: episodeTitle);
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 1);
        return cut;
    }

    [Fact]
    public async Task OutsideDeclaredSeason_PreselectsTheSuggestedEpisode()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        cut.Find("input[role=combobox]").GetAttribute("value").ShouldNotBeNull().ShouldContain("S03E04");
    }

    [Fact]
    public async Task OutsideDeclaredSeason_ShowsACautionNamingTheEpisodeAndDeclaredSeason()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        IElement note = cut.Find($"#{NoteId}");
        note.ClassList.ShouldContain("caution-strip");
        note.TextContent.ShouldContain("Caution:");
        note.TextContent.ShouldContain("Title matches S03E04, outside this program's season 5. Check before matching.");
    }

    [Fact]
    public async Task EpisodeNumber_ShowsTheTitleItWasSuggestedFrom()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(204, MatchSuggestionReason.EpisodeNumber), DefaultSeason: 2, DeclaredSeasons: []));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync("Þáttur 4");

        // Assert
        IElement note = cut.Find($"#{NoteId}");
        note.ClassList.ShouldNotContain("caution-strip");
        note.TextContent.Trim().ShouldBe("Suggested from the title \"Þáttur 4\": S02E04.");
        cut.Find("input[role=combobox]").GetAttribute("value").ShouldNotBeNull().ShouldContain("S02E04");
    }

    [Fact]
    public async Task TheComboboxIsDescribedByTheNote()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        cut.Find("input[role=combobox]").GetAttribute("aria-describedby").ShouldBe(NoteId);
    }

    [Fact]
    public async Task WhenAnotherEpisodeIsPicked_HidesTheNote()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Act
        await cut.Find("input[role=combobox]").InputAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Upphaf" });
        await cut.FindAll("li[role=option]").Single(li => li.TextContent.Contains("S05E01", StringComparison.Ordinal))
            .MouseDownAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        cut.FindAll($"#{NoteId}").ShouldBeEmpty();
        cut.Find("input[role=combobox]").HasAttribute("aria-describedby").ShouldBeFalse();
    }

    [Fact]
    public async Task WhenTheSuggestedEpisodeIsNotLoaded_SelectsNothingAndShowsNoNote()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(999, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        AssertNothingSuggested(cut);
    }

    [Fact]
    public async Task WhenThereIsNoSuggestion_SelectsNothingAndShowsNoNote()
    {
        // Arrange
        GivenSuggestion(new(Suggestion: null, DefaultSeason: 5, DeclaredSeasons: [5]));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        AssertNothingSuggested(cut);
    }

    [Fact]
    public async Task WhenTheQueryReturnsNothing_SelectsNothingAndShowsNoNote()
    {
        // Arrange
        GivenSuggestion(null);

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        AssertNothingSuggested(cut);
    }

    [Fact]
    public async Task WhenTheQueryFails_SelectsNothingAndShowsNoError()
    {
        // Arrange
        _suggestionHandler
            .Handle(Arg.Any<GetEpisodeMatchSuggestionQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("TVDB down"));

        // Act
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Assert
        AssertNothingSuggested(cut);
        cut.FindAll(".dialog-error").ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenTheEpisodeIsAlreadyMatched_DoesNotQueryForASuggestion()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));
        IRenderedComponent<MatchEpisodeDialog> cut = Render<MatchEpisodeDialog>();

        // Act
        await cut.Instance.OpenAsync("ruv-1", currentMatches: [new(501, 5, 1, SonarrEpisodeStatus.NotTracked, false, null)], tvdbSeriesId: 42, episodeTitle: "Heimkoman");
        await cut.WaitForStateAsync(() => cut.FindAll("input[role=combobox]").Count == 1);

        // Assert
        await _suggestionHandler.DidNotReceiveWithAnyArgs().Handle(default!, Xunit.TestContext.Current.CancellationToken);
        cut.FindAll($"#{NoteId}").ShouldBeEmpty();
    }

    [Fact]
    public async Task ConfirmingTheSuggestion_SubmitsAManualMatchForIt()
    {
        // Arrange
        GivenSuggestion(new(new SuggestedEpisode(304, MatchSuggestionReason.OutsideDeclaredSeason), DefaultSeason: 5, DeclaredSeasons: [5]));
        IRenderedComponent<MatchEpisodeDialog> cut = await OpenUnmatchedAsync();

        // Act
        await cut.Find(".dialog-button--primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        await _matchHandler.Received(1).Handle(
            Arg.Is<MatchEpisodeCommand>(c => c.RuvId == "ruv-1" && c.TvdbIds.Count == 1 && c.TvdbIds[0] == 304),
            Arg.Any<CancellationToken>());
    }

    private static void AssertNothingSuggested(IRenderedComponent<MatchEpisodeDialog> cut)
    {
        IElement input = cut.Find("input[role=combobox]");
        (input.GetAttribute("value") ?? string.Empty).ShouldBeEmpty();
        input.HasAttribute("aria-describedby").ShouldBeFalse();
        cut.FindAll($"#{NoteId}").ShouldBeEmpty();
    }
}
