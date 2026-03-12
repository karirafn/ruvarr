using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class MarkComplete
{
    [Fact]
    public void RemovesItemFromQueue()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
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
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.Items.ShouldHaveSingleItem();
    }
}
