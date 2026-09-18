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
    public void WhenItemIsProcessing_RecordsReRefreshRequestWithoutDisturbingInFlightPass()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        while (sut.TryLeaseNext() is IQueueLease drained)
        {
            _ = drained;
        }

        sut.MarkProcessing(2);

        // Act — user requests a manual refresh while item 2 is in-flight
        sut.PriorityEnqueue(2, "Program B");

        // Assert — item still shows as Processing (in-flight pass is undisturbed)
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[0].Status.ShouldBe(ProgramRefreshStatus.Processing);

        // Assert — the item is NOT re-leaseable while processing (no second lease available)
        IQueueLease? extraLease = sut.TryLeaseNext();
        extraLease.ShouldBeNull();
    }

    [Fact]
    public void WhenItemIsProcessing_RepeatedPriorityEnqueueIsIdempotent()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        while (sut.TryLeaseNext() is IQueueLease drained)
        {
            _ = drained;
        }

        sut.MarkProcessing(1);

        // Act — call PriorityEnqueue multiple times while item is processing
        sut.PriorityEnqueue(1, "Program A");
        sut.PriorityEnqueue(1, "Program A");
        sut.PriorityEnqueue(1, "Program A");

        // Assert — exactly one item remains in queue (re-refresh flag is a bool, not a counter)
        sut.Items.Count.ShouldBe(1);

        // After MarkComplete, exactly ONE follow-up entry should be leaseable
        sut.MarkComplete(1);

        IQueueLease? followUp = sut.TryLeaseNext();
        followUp.ShouldNotBeNull();

        IQueueLease? extraLease = sut.TryLeaseNext();
        extraLease.ShouldBeNull();
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
        using IQueueLease firstLease = sut.TryLeaseNext().ShouldNotBeNull();

        // Act
        sut.PriorityEnqueue(1, "Program A");

        // Assert — item was re-queued at front, read flag cleared
        using IQueueLease secondLease = sut.TryLeaseNext().ShouldNotBeNull();
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
