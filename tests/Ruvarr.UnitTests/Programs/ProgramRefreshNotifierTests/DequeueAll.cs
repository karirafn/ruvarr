using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class DequeueAll
{
    [Fact]
    public void ReturnsEmptyWhenEmpty()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        IEnumerable<int> result = sut.DequeueAll();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsAllEnqueuedIds()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        List<int> result = sut.DequeueAll().ToList();

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldContain(1);
        result.ShouldContain(2);
    }

    [Fact]
    public void DequeuesInFifoOrder()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.Enqueue(3, "Program C");

        // Act
        List<int> result = sut.DequeueAll().ToList();

        // Assert
        result[0].ShouldBe(1);
        result[1].ShouldBe(2);
        result[2].ShouldBe(3);
    }
}
