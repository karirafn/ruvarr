using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class CurrentProgram
{
    [Fact]
    public void ReturnsNull_WhenQueueIsEmpty()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void ReturnsNull_WhenNoItemIsProcessing()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void ReturnsProcessingItemName()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkProcessing(1);

        // Act & Assert
        sut.CurrentProgram.ShouldBe("Program A");
    }
}
