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
        // Arrange -- schedule a lookup first (LookupCount becomes 1) then match so the
        // episode is linked. ScheduleLookup no-ops once links exist, so the order matters.
        // Match sets NextLookup = null but does not reset LookupCount, giving us a non-zero
        // count to assert against -- the assertion would be vacuous if LookupCount were 0.
        RuvEpisodeBuilder builder = new RuvEpisodeBuilder();
        RuvEpisode sut = builder.BuildWithScheduledLookup();
        sut.Match(tvdbId: 42, season: 1, episode: 3, isMissing: false);

        int originalLookupCount = sut.LookupCount;
        DateTime? originalMatched = sut.Matched;
        int originalTvdbCount = sut.TvdbEpisodes.Count;

        originalLookupCount.ShouldBe(1);

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
