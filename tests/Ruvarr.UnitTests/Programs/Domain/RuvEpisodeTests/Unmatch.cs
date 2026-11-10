using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class Unmatch
{
    [Fact]
    public void ClearsLinksMatchedAndOrigin()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().BuildMatched();

        // Act
        sut.Unmatch();

        // Assert
        sut.ShouldSatisfyAllConditions(
            () => sut.TvdbEpisodes.ShouldBeEmpty(),
            () => sut.Matched.ShouldBeNull(),
            () => sut.MatchOrigin.ShouldBeNull());
    }

    [Fact]
    public void ResetsTheLookupSchedule()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().BuildWithScheduledLookup();
        sut.Match(tvdbId: 1, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Automatic);

        // Act
        sut.Unmatch();

        // Assert
        sut.ShouldSatisfyAllConditions(
            () => sut.LookupCount.ShouldBe(0),
            () => sut.NextLookup.ShouldBeNull());
    }
}
