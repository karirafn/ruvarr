using Ruvarr.Contracts;
using Ruvarr.Programs;

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
}
