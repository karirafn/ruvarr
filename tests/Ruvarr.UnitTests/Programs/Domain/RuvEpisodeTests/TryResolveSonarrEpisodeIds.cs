using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class TryResolveSonarrEpisodeIds
{
    [Fact]
    public void WhenAllLinksPresent_ReturnsTrueWithIdsInLinkOrder()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(tvdbId: 5001, seasonNumber: 1, episodeNumber: 1, isMissing: false),
            TvdbEpisode.Create(tvdbId: 5002, seasonNumber: 1, episodeNumber: 2, isMissing: false),
        ]);

        Dictionary<int, int> map = new()
        {
            [5001] = 201,
            [5002] = 202,
        };

        // Act
        bool result = sut.TryResolveSonarrEpisodeIds(map, out IReadOnlyList<int> sonarrEpisodeIds);

        // Assert
        result.ShouldBeTrue();
        sonarrEpisodeIds.ShouldBe([201, 202]);
    }

    [Fact]
    public void WhenOneLinkAbsent_ReturnsFalseWithEmptyOut()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(tvdbId: 5001, seasonNumber: 1, episodeNumber: 1, isMissing: false),
            TvdbEpisode.Create(tvdbId: 5002, seasonNumber: 1, episodeNumber: 2, isMissing: false),
        ]);

        Dictionary<int, int> map = new()
        {
            [5001] = 201,
        };

        // Act
        bool result = sut.TryResolveSonarrEpisodeIds(map, out IReadOnlyList<int> sonarrEpisodeIds);

        // Assert
        result.ShouldBeFalse();
        sonarrEpisodeIds.ShouldBeEmpty();
    }

    [Fact]
    public void WhenMapHasExtraEntries_ReturnsTrueWithOnlyMatchingIds()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.MatchMultiple(
        [
            TvdbEpisode.Create(tvdbId: 5001, seasonNumber: 1, episodeNumber: 1, isMissing: false),
            TvdbEpisode.Create(tvdbId: 5002, seasonNumber: 1, episodeNumber: 2, isMissing: false),
        ]);

        Dictionary<int, int> map = new()
        {
            [5001] = 201,
            [5002] = 202,
            [9001] = 301,
            [9002] = 302,
        };

        // Act
        bool result = sut.TryResolveSonarrEpisodeIds(map, out IReadOnlyList<int> sonarrEpisodeIds);

        // Assert
        result.ShouldBeTrue();
        sonarrEpisodeIds.Count.ShouldBe(2);
        sonarrEpisodeIds.ShouldBe([201, 202]);
    }

    [Fact]
    public void WhenLinksEmpty_ReturnsFalseWithEmptyOut()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();

        Dictionary<int, int> map = new()
        {
            [5001] = 201,
        };

        // Act
        bool result = sut.TryResolveSonarrEpisodeIds(map, out IReadOnlyList<int> sonarrEpisodeIds);

        // Assert
        result.ShouldBeFalse();
        sonarrEpisodeIds.ShouldBeEmpty();
    }
}
