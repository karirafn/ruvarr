using Ruvarr.Contracts;
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
        sut.Match(tvdbId: 42, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

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
        sut.Match(tvdbId: 1, season: 3, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

        // Assert
        sut.TvdbEpisodes[0].SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public void SetsEpisodeNumber()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 7, sonarrStatuses: SonarrStatuses.NotTracked);

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
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

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
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

        // Assert
        sut.NextLookup.ShouldBeNull();
    }

    [Fact]
    public void SetsSonarrStatusFromLookup()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.Missing(1));

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.Missing);
    }

    [Fact]
    public void SetsNotTracked_WhenEpisodeAbsentFromLookup()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Fact]
    public void SetsHasIslTranslationTrue()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, hasIslTranslation: true);

        // Assert
        sut.TvdbEpisodes[0].HasIslTranslation.ShouldBeTrue();
    }

    [Fact]
    public void SetsHasIslTranslationFalse()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, hasIslTranslation: false);

        // Assert
        sut.TvdbEpisodes[0].HasIslTranslation.ShouldBeFalse();
    }

    [Fact]
    public void ReplacesExistingMatches()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

        // Act
        sut.Match(tvdbId: 2, season: 2, episode: 5, sonarrStatuses: SonarrStatuses.NotTracked);

        // Assert
        sut.TvdbEpisodes.ShouldHaveSingleItem();
        sut.TvdbEpisodes[0].TvdbId.ShouldBe(2);
    }

    [Fact]
    public void SetsNotTracked_WhenLookupUnavailable()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrEpisodeStatuses.Unavailable);

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Fact]
    public void KeepsStoredStatusOfExistingLink_WhenLookupUnavailable()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.Of(1, SonarrEpisodeStatus.Downloaded));

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrEpisodeStatuses.Unavailable);

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.Downloaded);
    }

    [Fact]
    public void OverwritesStoredStatusOfExistingLink_WhenLookupSucceeds()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.Of(1, SonarrEpisodeStatus.Downloaded));

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked);

        // Assert
        sut.TvdbEpisodes[0].SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }
}