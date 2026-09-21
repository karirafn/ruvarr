using Ruvarr.Abstractions;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class Lease
{
    [Fact]
    public void WhenQueueIsEmpty_TryLeaseNextReturnsNull()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);

        // Act
        IQueueLease? lease = sut.TryLeaseNext();

        // Assert
        lease.ShouldBeNull();
    }

    [Fact]
    public void WhenQueueHasItem_TryLeaseNextReturnsLeaseWithCorrectRuvId()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(42, "Program A");

        // Act
        IQueueLease? lease = sut.TryLeaseNext();

        // Assert
        lease.ShouldNotBeNull();
        lease.RuvId.ShouldBe(42);
    }

    [Fact]
    public void WhenLeaseDisposed_ItemIsRemovedFromQueue()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(42, "Program A");
        IQueueLease lease = sut.TryLeaseNext().ShouldNotBeNull();

        // Act
        lease.Dispose();

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void WhenLeaseDisposedTwice_MarkCompleteCalledOnlyOnce()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        IQueueLease lease = sut.TryLeaseNext().ShouldNotBeNull();
        lease.Dispose(); // first dispose calls MarkComplete which increments CompletedCount to 1;
                         // the batch is not done (item 2 still queued) so there is no reset

        // Act — second dispose must be a no-op (CompletedCount must stay at 1, not increment again)
        lease.Dispose();

        // Assert
        sut.CompletedCount.ShouldBe(1); // first dispose incremented it to 1; second must not increment again
        sut.Items.ShouldContain(x => x.RuvId == 2); // item 2 still in queue
    }

    [Fact]
    public void WhenLastLeaseDisposed_BatchBookkeepingFires()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        IQueueLease lease1 = sut.TryLeaseNext().ShouldNotBeNull();
        IQueueLease lease2 = sut.TryLeaseNext().ShouldNotBeNull();
        lease1.Dispose();

        // Act — disposing the last lease should finalize the batch
        lease2.Dispose();

        // Assert
        sut.LastCompletedAt.ShouldNotBeNull();
        sut.LastRunDuration.ShouldNotBeNull();
        sut.LastRunTotal.ShouldBe(2);
        sut.CompletedCount.ShouldBe(0);
        sut.BatchStartedAt.ShouldBeNull();
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void WhenMultipleItemsEnqueued_LeasesReturnInFifoOrder()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.Enqueue(3, "Program C");

        // Act
        IQueueLease lease1 = sut.TryLeaseNext().ShouldNotBeNull();
        IQueueLease lease2 = sut.TryLeaseNext().ShouldNotBeNull();
        IQueueLease lease3 = sut.TryLeaseNext().ShouldNotBeNull();

        // Assert
        lease1.RuvId.ShouldBe(1);
        lease2.RuvId.ShouldBe(2);
        lease3.RuvId.ShouldBe(3);
    }

    [Fact]
    public void WhenAllItemsLeased_TryLeaseNextReturnsNull()
    {
        // Arrange
        ProgramRefreshNotifier sut = new(TimeProvider.System);
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        _ = sut.TryLeaseNext();
        _ = sut.TryLeaseNext();

        // Act
        IQueueLease? lease = sut.TryLeaseNext();

        // Assert
        lease.ShouldBeNull();
    }
}
