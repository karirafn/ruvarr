using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class Enqueue
{
    [Fact]
    public void AllowsDequeue()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
    }

    [Fact]
    public void DeduplicatesById()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program");

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
        sut.TryDequeue(out int _).ShouldBeFalse();
    }

    [Fact]
    public void AllowsReEnqueueAfterMarkComplete()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program");
        sut.TryDequeue(out int _);
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        sut.TryDequeue(out int _).ShouldBeTrue();
    }
}
