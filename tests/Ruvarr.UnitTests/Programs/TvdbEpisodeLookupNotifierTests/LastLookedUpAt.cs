using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class LastLookedUpAt
{
    [Fact]
    public void IsNull_Initially()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();

        // Act & Assert
        sut.LastLookedUpAt.ShouldBeNull();
    }

    [Fact]
    public void IsSet_AfterMarkComplete()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.LastLookedUpAt.ShouldNotBeNull();
    }

    [Fact]
    public void IsUpdated_OnSubsequentMarkComplete()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);
        DateTimeOffset? first = sut.LastLookedUpAt;

        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkComplete(2);

        // Assert
        sut.LastLookedUpAt.ShouldNotBeNull();
        sut.LastLookedUpAt!.Value.ShouldBeGreaterThanOrEqualTo(first!.Value);
    }
}
