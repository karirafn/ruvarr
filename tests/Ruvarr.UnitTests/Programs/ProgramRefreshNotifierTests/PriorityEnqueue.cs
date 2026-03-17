using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class PriorityEnqueue
{
    [Fact]
    public void PlacesItemAtFrontOfItems()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.PriorityEnqueue(3, "Program C");

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(3);
        items[0].RuvId.ShouldBe(3);
        items[1].RuvId.ShouldBe(1);
        items[2].RuvId.ShouldBe(2);
    }

    [Fact]
    public void MovesExistingQueuedItemToFront()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.Enqueue(3, "Program C");

        // Act
        sut.PriorityEnqueue(3, "Program C");

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(3);
        items[0].RuvId.ShouldBe(3);
        items[1].RuvId.ShouldBe(1);
        items[2].RuvId.ShouldBe(2);
    }

    [Fact]
    public void IsNoOpForItemInProcessingState()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        List<int> _ = sut.DequeueAll().ToList();
        sut.MarkProcessing(2);

        // Act
        sut.PriorityEnqueue(2, "Program B");

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[0].Status.ShouldBe(ProgramRefreshStatus.Processing);
    }

    [Fact]
    public void StandardEnqueueIsNoOpForPriorityQueuedItem()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.PriorityEnqueue(1, "Program A");

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.ShouldHaveSingleItem();
    }

    [Fact]
    public void ClearsReadFlagSoItemCanBeReadAgain()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        List<int> firstRead = sut.DequeueAll().ToList();
        firstRead.ShouldHaveSingleItem();

        // Act
        sut.PriorityEnqueue(1, "Program A");

        // Assert
        List<int> secondRead = sut.DequeueAll().ToList();
        secondRead.ShouldHaveSingleItem();
        secondRead[0].ShouldBe(1);
    }

    [Fact]
    public void DequeueOrderRespectsPriority()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.PriorityEnqueue(3, "Program C");

        // Act
        List<int> dequeued = sut.DequeueAll().ToList();

        // Assert
        dequeued[0].ShouldBe(3);
        dequeued[1].ShouldBe(1);
        dequeued[2].ShouldBe(2);
    }
}
