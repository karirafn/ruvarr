using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class Matches
{
    [Fact]
    public void WhenQueryMatchesSeasonCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "s02");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesEpisodeCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "e05");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesFullCode_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "s02e05");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenQueryMatchesNameFragment_ReturnsTrue()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Breaking Bad", SeasonNumber: 1, EpisodeNumber: 1);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "breaking");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenNameIsNullAndQueryDoesNotMatchCode_DoesNotThrow()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: null!, SeasonNumber: 1, EpisodeNumber: 1);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "pilot");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenQueryDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        bool result = EpisodeCombobox.Matches(episode, "xyz999");

        // Assert
        result.ShouldBeFalse();
    }
}
