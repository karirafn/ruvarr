using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class SeasonEpisodeLabel
{
    [Fact]
    public void WhenNoTvdbLinks_ReturnsNull()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        string? result = sut.SeasonEpisodeLabel;

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void WhenSingleLink_ReturnsSingleSeasonEpisodeLabel()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1234, season: 1, episode: 1, isMissing: false);

        // Act
        string? result = sut.SeasonEpisodeLabel;

        // Assert
        result.ShouldBe("S01E01");
    }

    [Fact]
    public void WhenMultipleLinks_ReturnsOrderedMultiEpisodeLabel()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(tvdbId: 5002, seasonNumber: 1, episodeNumber: 2, isMissing: false),
            TvdbEpisode.Create(tvdbId: 5001, seasonNumber: 1, episodeNumber: 1, isMissing: false),
        ]);

        // Act
        string? result = sut.SeasonEpisodeLabel;

        // Assert
        result.ShouldBe("S01E01E02");
    }
}
