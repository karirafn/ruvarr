using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class FilterAndOrder
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> AllEpisodes =
    [
        new(TvdbId: 101, Name: "Alpha", SeasonNumber: 1, EpisodeNumber: 1),
        new(TvdbId: 102, Name: "Beta", SeasonNumber: 1, EpisodeNumber: 2),
        new(TvdbId: 201, Name: "Gamma", SeasonNumber: 2, EpisodeNumber: 1),
        new(TvdbId: 202, Name: "Delta", SeasonNumber: 2, EpisodeNumber: 2),
        new(TvdbId: 301, Name: "Epsilon", SeasonNumber: 3, EpisodeNumber: 1),
    ];

    [Fact]
    public void WhenEmptyQueryAndNoSelection_ReturnsDefaultSeasonOnly()
    {
        // Arrange & Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            AllEpisodes,
            query: "",
            selectedEpisodeId: null,
            defaultSeason: 2);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(e => e.SeasonNumber == 2);
    }

    [Fact]
    public void WhenEmptyQueryAndSelectionExists_ReturnsSelectedEpisodeSeason()
    {
        // Arrange & Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            AllEpisodes,
            query: "",
            selectedEpisodeId: 301,
            defaultSeason: 1);

        // Assert
        result.Count.ShouldBe(1);
        result.ShouldAllBe(e => e.SeasonNumber == 3);
    }

    [Fact]
    public void WhenEmptyQueryAndSelectionExists_DoesNotUseDefaultSeason()
    {
        // Arrange & Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            AllEpisodes,
            query: "",
            selectedEpisodeId: 201,
            defaultSeason: 1);

        // Assert
        result.ShouldAllBe(e => e.SeasonNumber == 2);
        result.ShouldNotContain(e => e.SeasonNumber == 1);
    }

    [Fact]
    public void WhenNonEmptyQuery_ReturnsAllMatchingAcrossSeasons()
    {
        // Arrange & Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            AllEpisodes,
            query: "e01",
            selectedEpisodeId: null,
            defaultSeason: 1);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(e => e.SeasonNumber == 1 && e.EpisodeNumber == 1);
        result.ShouldContain(e => e.SeasonNumber == 2 && e.EpisodeNumber == 1);
        result.ShouldContain(e => e.SeasonNumber == 3 && e.EpisodeNumber == 1);
    }

    [Fact]
    public void ResultsAreOrderedBySeasonThenEpisode()
    {
        // Arrange
        IReadOnlyList<TvdbSeriesEpisode> unordered =
        [
            new(TvdbId: 301, Name: "Z", SeasonNumber: 3, EpisodeNumber: 1),
            new(TvdbId: 102, Name: "Y", SeasonNumber: 1, EpisodeNumber: 2),
            new(TvdbId: 101, Name: "X", SeasonNumber: 1, EpisodeNumber: 1),
            new(TvdbId: 201, Name: "W", SeasonNumber: 2, EpisodeNumber: 1),
        ];

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            unordered,
            query: "s",
            selectedEpisodeId: null,
            defaultSeason: null);

        // Assert
        result.Select(e => e.TvdbId).ShouldBe([101, 102, 201, 301]);
    }

    [Fact]
    public void WhenOver100Match_ReturnsFullSetBeforeCap()
    {
        // Arrange
        IReadOnlyList<TvdbSeriesEpisode> manyEpisodes = Enumerable
            .Range(1, 101)
            .Select(i => new TvdbSeriesEpisode(TvdbId: i, Name: $"Episode {i}", SeasonNumber: 1, EpisodeNumber: i))
            .ToList();

        // Act
        IReadOnlyList<TvdbSeriesEpisode> result = EpisodeCombobox.FilterAndOrder(
            manyEpisodes,
            query: "episode",
            selectedEpisodeId: null,
            defaultSeason: null);

        // Assert — FilterAndOrder returns full ordered set; cap is applied by the component
        result.Count.ShouldBe(101);

        IReadOnlyList<TvdbSeriesEpisode> capped = [.. result.Take(EpisodeCombobox.MaxResults)];
        capped.Count.ShouldBe(100);
    }
}
