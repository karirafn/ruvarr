using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class SetMissing
{
    [Fact]
    public void IsFalseByDefault()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Assert
        sut.TvdbEpisodes[0].IsMissing.ShouldBeFalse();
    }

    [Fact]
    public void SetsTrueWhenMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: false);

        // Act
        sut.UpdateMissingStatus(new HashSet<int> { 1 });

        // Assert
        sut.TvdbEpisodes[0].IsMissing.ShouldBeTrue();
    }

    [Fact]
    public void SetsFalseWhenNotMissing()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().Build();
        sut.Match(tvdbId: 1, season: 1, episode: 1, isMissing: true);

        // Act
        sut.UpdateMissingStatus([]);

        // Assert
        sut.TvdbEpisodes[0].IsMissing.ShouldBeFalse();
    }
}
