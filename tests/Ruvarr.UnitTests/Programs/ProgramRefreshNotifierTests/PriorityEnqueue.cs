using Ruvarr.Abstractions;
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
        // Drain the read-set so MarkProcessing reflects as processing in Items
        while (sut.TryLeaseNext() is IQueueLease drained)
        {
            _ = drained;
        }
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
    public void ClearsReadFlagSoItemCanBeLeasedAgain()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        IQueueLease? firstLease = sut.TryLeaseNext();
        firstLease.ShouldNotBeNull();

        // Act
        sut.PriorityEnqueue(1, "Program A");

        // Assert — item was re-queued at front, read flag cleared
        IQueueLease? secondLease = sut.TryLeaseNext();
        secondLease.ShouldNotBeNull();
        secondLease.RuvId.ShouldBe(1);
    }

    [Fact]
    public void LeaseOrderRespectsPriority()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.PriorityEnqueue(3, "Program C");

        // Act
        IQueueLease lease1 = sut.TryLeaseNext().ShouldNotBeNull();
        IQueueLease lease2 = sut.TryLeaseNext().ShouldNotBeNull();
        IQueueLease lease3 = sut.TryLeaseNext().ShouldNotBeNull();

        // Assert
        lease1.RuvId.ShouldBe(3);
        lease2.RuvId.ShouldBe(1);
        lease3.RuvId.ShouldBe(2);
    }
}
