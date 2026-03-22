using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class Match
{
    [Fact]
    public void AddsTvdbEpisodeToCollection()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 42, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.TvdbEpisodes.ShouldHaveSingleItem();
        sut.TvdbEpisodes[0].TvdbId.ShouldBe(42);
    }

    [Fact]
    public void SetsSeasonNumber()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 3, episode: 1, isMissing: false);

        // Assert
        sut.TvdbEpisodes[0].SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public void SetsEpisodeNumber()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 7, isMissing: false);

        // Assert
        sut.TvdbEpisodes[0].EpisodeNumber.ShouldBe(7);
    }

    [Fact]
    public void SetsMatchedToUtcNow()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.Matched.ShouldNotBeNull();
        sut.Matched.Value.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void ClearsNextLookup()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.ScheduleLookup();
        sut.NextLookup.ShouldNotBeNull();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.NextLookup.ShouldBeNull();
    }

    [Fact]
    public void SetsIsMissingTrue()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);

        // Assert
        sut.TvdbEpisodes[0].IsMissing.ShouldBeTrue();
    }

    [Fact]
    public void SetsIsMissingFalse()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.TvdbEpisodes[0].IsMissing.ShouldBeFalse();
    }

    [Fact]
    public void ReplacesExistingMatches()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Act
        sut.Match(tvdbId: 2, season: 2, episode: 5, isMissing: false);

        // Assert
        sut.TvdbEpisodes.ShouldHaveSingleItem();
        sut.TvdbEpisodes[0].TvdbId.ShouldBe(2);
    }
}
