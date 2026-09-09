using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class UpdateTitle
{
    [Fact]
    public void WhenTitleDiffers_OverwritesTitle()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().WithTitle("Original Title").Build();

        // Act
        sut.UpdateTitle("Updated Title");

        // Assert
        sut.Title.ShouldBe("Updated Title");
    }

    [Fact]
    public void WhenEpisodeIsUnlinked_ResetsLookupCountAndNextLookup()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().BuildWithScheduledLookup();
        sut.LookupCount.ShouldBeGreaterThan(0);
        sut.NextLookup.ShouldNotBeNull();

        // Act
        sut.UpdateTitle("New Title");

        // Assert
        sut.ShouldSatisfyAllConditions(
            () => sut.LookupCount.ShouldBe(0),
            () => sut.NextLookup.ShouldBeNull());
    }

    [Fact]
    public void WhenEpisodeIsLinked_PreservesLookupStateAndLinks()
    {
        // Arrange
        RuvEpisode sut = new RuvEpisodeBuilder().BuildMatched(tvdbId: 42, season: 1, episodeNumber: 3);
        int originalLookupCount = sut.LookupCount;
        DateTime? originalMatched = sut.Matched;
        int originalTvdbCount = sut.TvdbEpisodes.Count;

        // Act
        sut.UpdateTitle("New Title");

        // Assert
        sut.ShouldSatisfyAllConditions(
            () => sut.LookupCount.ShouldBe(originalLookupCount),
            () => sut.NextLookup.ShouldBeNull(),
            () => sut.Matched.ShouldBe(originalMatched),
            () => sut.TvdbEpisodes.Count.ShouldBe(originalTvdbCount));
    }
}
