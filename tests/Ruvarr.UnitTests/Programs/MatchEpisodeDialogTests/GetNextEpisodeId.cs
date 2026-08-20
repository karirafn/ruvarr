using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchEpisodeDialogTests;

public sealed class GetNextEpisodeId
{
    [Fact]
    public void ReturnsNextEpisodeWhenCurrentIsInTheMiddle()
    {
        // Arrange
        List<TvdbSeriesEpisode> episodes =
        [
            new(100, "Episode 1", 1, 1),
            new(101, "Episode 2", 1, 2),
            new(102, "Episode 3", 1, 3),
        ];

        // Act
        int? result = MatchEpisodeDialog.GetNextEpisodeId(episodes, currentEpisodeId: 101);

        // Assert
        result.ShouldBe(102);
    }

    [Fact]
    public void ReturnsNullWhenCurrentIsLastEpisode()
    {
        // Arrange
        List<TvdbSeriesEpisode> episodes =
        [
            new(100, "Episode 1", 1, 1),
            new(101, "Episode 2", 1, 2),
        ];

        // Act
        int? result = MatchEpisodeDialog.GetNextEpisodeId(episodes, currentEpisodeId: 101);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNullWhenCurrentEpisodeIdIsNull()
    {
        // Arrange
        List<TvdbSeriesEpisode> episodes =
        [
            new(100, "Episode 1", 1, 1),
            new(101, "Episode 2", 1, 2),
        ];

        // Act
        int? result = MatchEpisodeDialog.GetNextEpisodeId(episodes, currentEpisodeId: null);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNullWhenCurrentEpisodeIdNotFoundInList()
    {
        // Arrange
        List<TvdbSeriesEpisode> episodes =
        [
            new(100, "Episode 1", 1, 1),
            new(101, "Episode 2", 1, 2),
        ];

        // Act
        int? result = MatchEpisodeDialog.GetNextEpisodeId(episodes, currentEpisodeId: 999);

        // Assert
        result.ShouldBeNull();
    }
}
