using Ruvarr.Contracts;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class PriorityEnqueue
{
    [Fact]
    public void WhenItemIsProcessing_RecordsReRefreshWithoutDisturbingInFlightPass()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.TryDequeue(out int ruvId);
        sut.MarkProcessing(ruvId);

        // Act — user requests a manual refresh while item is in-flight
        sut.PriorityEnqueue(ruvId, "Program A");

        // Assert — item still shows as Processing (in-flight pass is undisturbed)
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = sut.Items;
        items.ShouldHaveSingleItem();
        items[0].Status.ShouldBe(TvdbSeriesLookupStatus.Processing);
    }

    [Fact]
    public void WhenItemIsProcessing_PriorityEnqueue_MarkComplete_ReQueuesItemAsPending()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.TryDequeue(out int ruvId);
        sut.MarkProcessing(ruvId);
        sut.PriorityEnqueue(ruvId, "Program A");

        // Act
        sut.MarkComplete(ruvId);

        // Assert — item is re-queued as a fresh Pending entry
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = sut.Items;
        items.ShouldHaveSingleItem();
        items[0].RuvId.ShouldBe(ruvId);
        items[0].Status.ShouldBe(TvdbSeriesLookupStatus.Pending);
    }

    [Fact]
    public void WhenItemIsProcessing_PriorityEnqueue_MarkComplete_ExactlyOneFollowUpDequeueable()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.TryDequeue(out int ruvId);
        sut.MarkProcessing(ruvId);
        sut.PriorityEnqueue(ruvId, "Program A");

        // Act
        sut.MarkComplete(ruvId);

        // Assert — exactly one follow-up entry is dequeueable
        bool hasFollowUp = sut.TryDequeue(out int followUpId);
        hasFollowUp.ShouldBeTrue();
        followUpId.ShouldBe(ruvId);

        bool hasExtra = sut.TryDequeue(out int _);
        hasExtra.ShouldBeFalse();
    }
}
