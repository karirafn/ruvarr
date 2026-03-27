using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.TvdbEpisodeTests;

public sealed class Create
{
    [Fact]
    public void SetsHasIslTranslationTrue()
    {
        // Arrange & Act
        TvdbEpisode sut = TvdbEpisode.Create(tvdbId: 1, seasonNumber: 1, episodeNumber: 1, isMissing: false, hasIslTranslation: true);

        // Assert
        sut.HasIslTranslation.ShouldBeTrue();
    }

    [Fact]
    public void SetsHasIslTranslationFalse()
    {
        // Arrange & Act
        TvdbEpisode sut = TvdbEpisode.Create(tvdbId: 1, seasonNumber: 1, episodeNumber: 1, isMissing: false, hasIslTranslation: false);

        // Assert
        sut.HasIslTranslation.ShouldBeFalse();
    }
}
