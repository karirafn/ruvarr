using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class Items
{
    [Fact]
    public void IsEmptyWhenNothingEnqueued()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;

        // Assert
        items.ShouldBeEmpty();
    }

    [Fact]
    public void ContainsEnqueuedItemWithPendingStatus()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        ProgramRefreshQueueItemSummary item = sut.Items.ShouldHaveSingleItem();
        item.RuvId.ShouldBe(1);
        item.ProgramName.ShouldBe("Program A");
        item.Status.ShouldBe(ProgramRefreshStatus.Pending);
    }

    [Fact]
    public void IsEmptyAfterMarkComplete()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        List<int> _ = sut.DequeueAll().ToList();

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.Items.ShouldBeEmpty();
    }

    [Fact]
    public void ReturnsPendingItemsInEnqueueOrder()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        sut.Enqueue(2, "Program B");
        sut.Enqueue(1, "Program A");

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }

    [Fact]
    public void ReturnsProcessingItemBeforePendingItems()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkProcessing(2);

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = sut.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(2);
        items[1].RuvId.ShouldBe(1);
    }
}
