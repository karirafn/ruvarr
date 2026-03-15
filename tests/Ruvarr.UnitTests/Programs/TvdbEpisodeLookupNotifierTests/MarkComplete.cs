using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class MarkComplete
{
    [Fact]
    public void RemovesItemFromQueue()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AllowsReEnqueueAfterComplete()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.Items.ShouldHaveSingleItem();
    }
}
