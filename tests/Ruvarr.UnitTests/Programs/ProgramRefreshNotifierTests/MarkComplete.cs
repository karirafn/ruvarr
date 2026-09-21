using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class MarkComplete
{
    [Fact]
    public void RemovesItemFromQueue()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void AllowsReEnqueueAfterComplete()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public void WhenRefreshAgainFlagSet_ReEnqueuesItemAsPendingAtFront()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        while (sut.TryLeaseNext() is not null)
        {
            // discard — drain the read set without disposing leases
        }

        sut.MarkProcessing(1);
        sut.PriorityEnqueue(1, "Program A"); // sets RefreshAgain flag

        // Act
        sut.MarkComplete(1);

        // Assert — item 1 is re-queued as Pending at the front
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(1);
        items[0].Status.ShouldBe(ProgramRefreshStatus.Pending);
    }

    [Fact]
    public void WhenRefreshAgainFlagSet_ReEnqueuedItemIsLeaseable()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        while (sut.TryLeaseNext() is not null)
        {
            // discard — drain the read set without disposing leases
        }

        sut.MarkProcessing(1);
        sut.PriorityEnqueue(1, "Program A"); // sets RefreshAgain flag

        // Act
        sut.MarkComplete(1);

        // Assert — exactly one follow-up lease is available
        IQueueLease? followUp = sut.TryLeaseNext();
        followUp.ShouldNotBeNull();
        followUp.RuvId.ShouldBe(1);

        IQueueLease? extra = sut.TryLeaseNext();
        extra.ShouldBeNull();
    }

    [Fact]
    public void WhenRefreshAgainFlagNotSet_RemovesItemNormally()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        while (sut.TryLeaseNext() is not null)
        {
            // discard — drain the read set without disposing leases
        }

        sut.MarkProcessing(1);

        // Act — no PriorityEnqueue during processing
        sut.MarkComplete(1);

        // Assert — item is gone
        sut.Items.ShouldBeEmpty();
        sut.TryLeaseNext().ShouldBeNull();
    }
}
