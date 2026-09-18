using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class ReRefresh
{
    [Fact]
    public void WhenPriorityEnqueuedMidDrainAllPass_ProducesExactlyOneFollowUpAfterCompletion()
    {
        // Arrange — mirror the exact call order RuvEpisodesSyncJob uses:
        //   drain all leases, then MarkProcessing per item, then Dispose (MarkComplete) per item
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Drain — collect all leases before processing starts
        List<IQueueLease> leases = [];
#pragma warning disable CA2000 // Leases disposed explicitly below
        while (sut.TryLeaseNext() is IQueueLease lease)
        {
            leases.Add(lease);
        }
#pragma warning restore CA2000

        leases.Count.ShouldBe(2);

        // Mark both as processing (as the job does)
        sut.MarkProcessing(leases[0].RuvId); // RuvId 1
        sut.MarkProcessing(leases[1].RuvId); // RuvId 2

        // Simulate user requesting manual refresh for item 2 while it is in-flight
        sut.PriorityEnqueue(2, "Program B");

        // Assert (mid-Arrange) — in-flight pass undisturbed; no extra lease available mid-pass
        IReadOnlyList<ProgramRefreshQueueItemSummary> midPassItems = sut.Items;
        midPassItems.ShouldContain(x => x.RuvId == 2 && x.Status == ProgramRefreshStatus.Processing);
        sut.TryLeaseNext().ShouldBeNull();

        // Act — complete both in-flight passes (dispose/MarkComplete in job order)
        leases[0].Dispose(); // completes item 1 — items still remain (item 2 still processing)
        leases[1].Dispose(); // completes item 2 — triggers re-queue because RefreshAgain=true

        // Assert — exactly one follow-up leaseable entry for item 2 at the front
        IReadOnlyList<ProgramRefreshQueueItemSummary> followUpItems = sut.Items;
        followUpItems.Count.ShouldBe(1);
        followUpItems[0].RuvId.ShouldBe(2);
        followUpItems[0].Status.ShouldBe(ProgramRefreshStatus.Pending);

        IQueueLease? followUpLease = sut.TryLeaseNext();
        followUpLease.ShouldNotBeNull();
        followUpLease.RuvId.ShouldBe(2);

        // No further leases available after the single follow-up
        sut.TryLeaseNext().ShouldBeNull();
    }

    [Fact]
    public void WhenItemCompletesBeforePriorityEnqueue_PriorityEnqueueAddsItBack()
    {
        // Arrange — edge case: item completes between user click and PriorityEnqueue call
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");

        IQueueLease? lease = sut.TryLeaseNext();
        lease.ShouldNotBeNull();

        // Item completes (removed from queue) before the PriorityEnqueue arrives
        sut.MarkProcessing(1);
        lease.Dispose(); // removes item 1 — no RefreshAgain flag set yet

        // Act — PriorityEnqueue arrives after completion; falls through to the new-item branch
        sut.PriorityEnqueue(1, "Program A");

        // Assert — item is present as a fresh Pending entry at the front; one lease available
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(1);
        items[0].RuvId.ShouldBe(1);
        items[0].Status.ShouldBe(ProgramRefreshStatus.Pending);

        IQueueLease? freshLease = sut.TryLeaseNext();
        freshLease.ShouldNotBeNull();
        freshLease.RuvId.ShouldBe(1);

        sut.TryLeaseNext().ShouldBeNull();
    }
}
