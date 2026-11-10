using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class MatchOriginTests
{
    [Fact]
    public void UnmatchedEpisodeHasNoOrigin()
    {
        // Act
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Assert
        sut.MatchOrigin.ShouldBeNull();
    }

    [Fact]
    public void MatchRecordsAutomaticOrigin() => AssertMatchRecords(MatchOrigin.Automatic);

    [Fact]
    public void MatchRecordsManualOrigin() => AssertMatchRecords(MatchOrigin.Manual);

    private static void AssertMatchRecords(MatchOrigin origin)
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: origin);

        // Assert
        sut.MatchOrigin.ShouldBe(origin);
    }

    [Fact]
    public void MatchMultipleRecordsTheOrigin()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        // Act
        sut.MatchMultiple([TvdbEpisode.Create(1, 1, 1), TvdbEpisode.Create(2, 1, 2)], SonarrStatuses.NotTracked, MatchOrigin.Manual);

        // Assert
        sut.MatchOrigin.ShouldBe(MatchOrigin.Manual);
    }

    [Fact]
    public void RematchReplacesTheOrigin()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Automatic);

        // Act
        sut.Match(tvdbId: 2, season: 1, episode: 2, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Manual);

        // Assert
        sut.MatchOrigin.ShouldBe(MatchOrigin.Manual);
    }
}
