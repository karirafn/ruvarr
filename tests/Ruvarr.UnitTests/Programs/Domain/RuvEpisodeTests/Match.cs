using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class Match
{
    [Fact]
    public void SetsTvdbId()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 42, season: 1, episode: 1);

        // Assert
        sut.TvdbId.ShouldBe(42);
    }

    [Fact]
    public void SetsSeasonNumber()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 3, episode: 1);

        // Assert
        sut.SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public void SetsEpisodeNumber()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 7);

        // Assert
        sut.EpisodeNumber.ShouldBe(7);
    }

    [Fact]
    public void SetsMatchedToUtcNow()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1);

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
        sut.Match(tvdbId: 1, season: 1, episode: 1);

        // Assert
        sut.NextLookup.ShouldBeNull();
    }
}
