using Ruvarr.Contracts;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class Items
{
    [Fact]
    public void IsEmptyWhenNothingEnqueued()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();

        // Act
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = sut.Items;

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public void ContainsEnqueuedItemWithPendingStatus()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        TvdbEpisodeLookupQueueItemSummary item = sut.Items.ShouldHaveSingleItem();
        item.RuvId.ShouldBe(1);
        item.ProgramName.ShouldBe("Program A");
        item.Status.ShouldBe(TvdbEpisodeLookupStatus.Pending);
    }

    [Fact]
    public void IsEmptyAfterMarkComplete()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
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
        TvdbEpisodeLookupNotifier sut = new();

        // Act
        sut.Enqueue(2, "Program B");
        sut.Enqueue(1, "Program A");

        // Assert
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }

    [Fact]
    public void ReturnsProcessingItemBeforePendingItems()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkProcessing(2);

        // Assert
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }
}
