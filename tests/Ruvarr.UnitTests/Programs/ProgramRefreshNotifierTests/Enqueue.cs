using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class Enqueue
{
    [Fact]
    public void AllowsDequeue()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        List<int> dequeued = sut.DequeueAll().ToList();
        dequeued.ShouldHaveSingleItem();
    }

    [Fact]
    public void DeduplicatesById()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program");

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        List<int> dequeued = sut.DequeueAll().ToList();
        dequeued.Count.ShouldBe(1);
    }

    [Fact]
    public void AllowsReEnqueueAfterMarkComplete()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program");
        List<int> _ = sut.DequeueAll().ToList();
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program");

        // Assert
        List<int> dequeued = sut.DequeueAll().ToList();
        dequeued.ShouldHaveSingleItem();
    }
}
