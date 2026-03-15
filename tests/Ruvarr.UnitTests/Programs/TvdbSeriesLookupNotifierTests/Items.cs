using Ruvarr.Contracts;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class Items
{
    [Fact]
    public void IsEmptyWhenNothingEnqueued()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = sut.Items;

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public void ContainsEnqueuedItemWithPendingStatus()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        TvdbSeriesLookupQueueItemSummary item = sut.Items.ShouldHaveSingleItem();
        item.RuvId.ShouldBe(1);
        item.ProgramName.ShouldBe("Program A");
        item.Status.ShouldBe(TvdbSeriesLookupStatus.Pending);
    }

    [Fact]
    public void IsEmptyAfterMarkComplete()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.TryDequeue(out int _);

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsPendingItemsInEnqueueOrder()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();

        // Act
        sut.Enqueue(2, "Program B");
        sut.Enqueue(1, "Program A");

        // Assert
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }

    [Fact]
    public void ReturnsProcessingItemBeforePendingItems()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkProcessing(2);

        // Assert
        IReadOnlyList<TvdbSeriesLookupQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }
}
