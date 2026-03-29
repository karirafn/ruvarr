using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class CurrentProgram
{
    [Fact]
    public void ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_WhenNoItemIsProcessing()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void ReturnsProcessingItemName()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkProcessing(1);

        // Act & Assert
        sut.CurrentProgram.ShouldBe("Program A");
    }
}
