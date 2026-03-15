using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class TryDequeue
{
    [Fact]
    public void ReturnsFalseWhenEmpty()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act
        bool result = sut.TryDequeue(out int _);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueAndIdWhenEnqueued()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(42, "Program");

        // Act
        bool result = sut.TryDequeue(out int ruvId);

        // Assert
        result.ShouldBeTrue();
        ruvId.ShouldBe(42);
    }

    [Fact]
    public void DequeuesInFifoOrder()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.Enqueue(3, "Program C");

        // Act / Assert
        sut.TryDequeue(out int first);
        sut.TryDequeue(out int second);
        sut.TryDequeue(out int third);

        first.ShouldBe(1);
        second.ShouldBe(2);
        third.ShouldBe(3);
    }
}
