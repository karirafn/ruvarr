using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class Enqueue
{
    [Fact]
    public void AllowsDequeue()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
    }

    [Fact]
    public void DeduplicatesById()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
        sut.TryDequeue(out int _).ShouldBeFalse();
    }

    [Fact]
    public void AllowsReEnqueueAfterMarkComplete()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.TryDequeue(out int _);
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
    }
}
