using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class BuildLabel
{
    [Fact]
    public void WhenEpisodeHasName_ReturnsFormattedLabel()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "Pilot", SeasonNumber: 2, EpisodeNumber: 5);

        // Act
        string result = EpisodeCombobox.BuildLabel(episode);

        // Assert
        result.ShouldBe("S02E05 · Pilot");
    }

    [Fact]
    public void WhenNameIsNull_ReturnsBareCode()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: null!, SeasonNumber: 1, EpisodeNumber: 3);

        // Act
        string result = EpisodeCombobox.BuildLabel(episode);

        // Assert
        result.ShouldBe("S01E03");
    }

    [Fact]
    public void WhenNameIsBlank_ReturnsBareCode()
    {
        // Arrange
        TvdbSeriesEpisode episode = new(TvdbId: 1, Name: "   ", SeasonNumber: 3, EpisodeNumber: 12);

        // Act
        string result = EpisodeCombobox.BuildLabel(episode);

        // Assert
        result.ShouldBe("S03E12");
    }
}
