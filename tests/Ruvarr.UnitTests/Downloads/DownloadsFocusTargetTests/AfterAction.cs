using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.DownloadsFocusTargetTests;

public sealed class AfterAction
{
    [Fact]
    public void ReturnsNextActionableRowsRetry_WhenRowWasRemoved()
    {
        // Arrange
        DownloadQueueItemSummary[] items = [Item("EP2", DownloadQueueStatus.Pending), Item("EP3", DownloadQueueStatus.Exhausted)];

        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction(items, "EP1", DownloadRowAction.Dismiss, 0);

        // Assert
        target.ShouldBe(new DownloadsFocusTarget.RowButton("EP3", DownloadRowAction.Retry));
    }

    [Fact]
    public void ReturnsNearestPrecedingActionableRow_WhenNoneFollow()
    {
        // Arrange
        DownloadQueueItemSummary[] items = [Item("EP1", DownloadQueueStatus.Failed), Item("EP2", DownloadQueueStatus.Failed), Item("EP4", DownloadQueueStatus.Pending)];

        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction(items, "EP3", DownloadRowAction.Dismiss, 2);

        // Assert
        target.ShouldBe(new DownloadsFocusTarget.RowButton("EP2", DownloadRowAction.Retry));
    }

    [Fact]
    public void ReturnsHeading_WhenNoActionableRowsRemain()
    {
        // Arrange
        DownloadQueueItemSummary[] items = [Item("EP2", DownloadQueueStatus.Pending), Item("EP3", DownloadQueueStatus.Downloading)];

        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction(items, "EP1", DownloadRowAction.Dismiss, 0);

        // Assert
        target.ShouldBeOfType<DownloadsFocusTarget.Heading>();
    }

    [Fact]
    public void ReturnsHeading_WhenQueueIsEmpty()
    {
        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction([], "EP1", DownloadRowAction.Dismiss, 3);

        // Assert
        target.ShouldBeOfType<DownloadsFocusTarget.Heading>();
    }

    [Fact]
    public void ReturnsInitiatingButton_WhenRowStillHasActions()
    {
        // Arrange
        DownloadQueueItemSummary[] items = [Item("EP0", DownloadQueueStatus.Failed), Item("EP1", DownloadQueueStatus.Failed)];

        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction(items, "EP1", DownloadRowAction.Dismiss, 1);

        // Assert
        target.ShouldBe(new DownloadsFocusTarget.RowButton("EP1", DownloadRowAction.Dismiss));
    }

    [Fact]
    public void ReturnsNextRow_WhenRetriedRowBecamePending()
    {
        // Arrange
        DownloadQueueItemSummary[] items = [Item("EP1", DownloadQueueStatus.Pending), Item("EP2", DownloadQueueStatus.Failed)];

        // Act
        DownloadsFocusTarget target = DownloadsFocusTarget.AfterAction(items, "EP1", DownloadRowAction.Retry, 0);

        // Assert
        target.ShouldBe(new DownloadsFocusTarget.RowButton("EP2", DownloadRowAction.Retry));
    }

    private static DownloadQueueItemSummary Item(string episodeRuvId, DownloadQueueStatus status) =>
        new(episodeRuvId, 42, "Show", "Title", status, null, 1, null, DateTime.UtcNow);
}
