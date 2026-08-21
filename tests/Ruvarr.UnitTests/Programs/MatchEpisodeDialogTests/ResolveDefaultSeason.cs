using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class ResolveDefaultSeason
{
    [Fact]
    public void ReturnsLowestMatchedSeasonFromSiblings()
    {
        // Arrange
        List<EpisodeSummary> siblings =
        [
            CreateEpisode(seasonNumber: 2),
            CreateEpisode(seasonNumber: 3),
        ];
        List<int> availableSeasons = [1, 2, 3];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void IgnoresSeasonZeroFromSiblings()
    {
        // Arrange
        List<EpisodeSummary> siblings =
        [
            CreateEpisode(seasonNumber: 0),
            CreateEpisode(seasonNumber: 2),
        ];
        List<int> availableSeasons = [1, 2];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void FallsBackToFirstAvailableSeasonWhenNoSiblingMatches()
    {
        // Arrange
        List<EpisodeSummary> siblings = [];
        List<int> availableSeasons = [1, 2, 3];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void FallsBackToFirstAvailableSeasonWhenSiblingSeasonNotInAvailable()
    {
        // Arrange
        List<EpisodeSummary> siblings =
        [
            CreateEpisode(seasonNumber: 5),
        ];
        List<int> availableSeasons = [1, 2, 3];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void ReturnsNullWhenNoAvailableSeasons()
    {
        // Arrange
        List<EpisodeSummary> siblings = [];
        List<int> availableSeasons = [];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void FallsBackToFirstAvailableWhenAllSiblingsUnmatched()
    {
        // Arrange
        List<EpisodeSummary> siblings =
        [
            CreateEpisode(seasonNumber: null),
            CreateEpisode(seasonNumber: null),
        ];
        List<int> availableSeasons = [2, 3];

        // Act
        int? result = MatchEpisodeDialog.ResolveDefaultSeason(siblings, availableSeasons);

        // Assert
        result.ShouldBe(2);
    }

    private static EpisodeSummary CreateEpisode(int? seasonNumber)
    {
        IReadOnlyList<TvdbEpisodeSummary> matches = seasonNumber is not null
            ? [new TvdbEpisodeSummary(100, seasonNumber.Value, 1, false, false, null)]
            : [];

        return new(
            EpisodeTitle: "Test",
            EpisodeRuvId: Guid.NewGuid().ToString()[..6],
            EpisodeDescription: "",
            TvdbMatches: matches,
            FirstRun: DateTime.UtcNow,
            RuvUrl: null,
            Duration: TimeSpan.Zero);
    }
}
