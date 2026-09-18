using Ruvarr.Abstractions;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshNotifierTests;

public sealed class BatchTracking
{
    [Fact]
    public void Enqueue_WhenQueueWasEmpty_SetsBatchStartedAt()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act
        sut.Enqueue(1, "Program A");

        // Assert
        sut.BatchStartedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Enqueue_WhenQueueAlreadyHadItems_DoesNotResetBatchStartedAt()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        DateTimeOffset? firstBatchStart = sut.BatchStartedAt;

        // Act
        sut.Enqueue(2, "Program B");

        // Assert
        sut.BatchStartedAt.ShouldBe(firstBatchStart);
    }

    [Fact]
    public void CompletedCount_InitiallyZero()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act & Assert
        sut.CompletedCount.ShouldBe(0);
    }

    [Fact]
    public void MarkComplete_IncrementsCompletedCount()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.CompletedCount.ShouldBe(1);
    }

    [Fact]
    public void MarkComplete_WhenLastItemCompleted_RecordsLastRunStats()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");
        sut.MarkComplete(1);

        // Act
        sut.MarkComplete(2);

        // Assert
        sut.LastCompletedAt.ShouldNotBeNull();
        sut.LastRunDuration.ShouldNotBeNull();
        sut.LastRunTotal.ShouldBe(2);
        sut.CompletedCount.ShouldBe(0);
        sut.BatchStartedAt.ShouldBeNull();
    }

    [Fact]
    public void MarkComplete_WhenItemsRemain_DoesNotRecordLastRunStats()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.Enqueue(2, "Program B");

        // Act
        sut.MarkComplete(1);

        // Assert
        sut.LastCompletedAt.ShouldBeNull();
        sut.LastRunDuration.ShouldBeNull();
        sut.LastRunTotal.ShouldBeNull();
    }

    [Fact]
    public void NewBatch_AfterPreviousBatchCompleted_TracksNewBatch()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkComplete(1);
        DateTimeOffset? firstLastCompleted = sut.LastCompletedAt;

        // Act
        sut.Enqueue(2, "Program B");
        sut.Enqueue(3, "Program C");
        sut.MarkComplete(2);

        // Assert
        sut.CompletedCount.ShouldBe(1);
        sut.BatchStartedAt.ShouldNotBeNull();
        sut.LastCompletedAt.ShouldBe(firstLastCompleted);
    }

    [Fact]
    public void CurrentProgram_ReturnsProcessingItemName()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        sut.MarkProcessing(1);

        // Act & Assert
        sut.CurrentProgram.ShouldBe("Program A");
    }

    [Fact]
    public void CurrentProgram_ReturnsNull_WhenNoItemProcessing()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void CurrentProgram_ReturnsNull_WhenQueueEmpty()
    {
        // Arrange
        ProgramRefreshNotifier sut = new();

        // Act & Assert
        sut.CurrentProgram.ShouldBeNull();
    }

    [Fact]
    public void WhenRefreshAgainFlagSet_BatchStatsAreDeferredUntilFollowUpCompletes()
    {
        // Arrange — enqueue one item, process it, then request a re-refresh mid-pass
        ProgramRefreshNotifier sut = new();
        sut.Enqueue(1, "Program A");
        while (sut.TryLeaseNext() is IQueueLease drained)
        {
            _ = drained;
        }

        sut.MarkProcessing(1);
        sut.PriorityEnqueue(1, "Program A"); // sets RefreshAgain flag

        // Act — complete the first pass; the item is re-queued (batch not yet finalised)
        sut.MarkComplete(1);

        // Assert — batch stats NOT yet finalised because a follow-up item remains
        sut.LastCompletedAt.ShouldBeNull();
        sut.LastRunDuration.ShouldBeNull();
        sut.LastRunTotal.ShouldBeNull();
        sut.CompletedCount.ShouldBe(1);
        sut.BatchStartedAt.ShouldNotBeNull();

        // Arrange — lease and complete the follow-up pass
        IQueueLease? followUp = sut.TryLeaseNext();
        followUp.ShouldNotBeNull();
        sut.MarkProcessing(followUp.RuvId);

        // Act — complete the follow-up pass; now the batch should finalise
        sut.MarkComplete(followUp.RuvId);

        // Assert — batch finalised after the follow-up pass
        sut.LastCompletedAt.ShouldNotBeNull();
        sut.LastRunDuration.ShouldNotBeNull();
        sut.LastRunTotal.ShouldBe(2);
        sut.CompletedCount.ShouldBe(0);
        sut.BatchStartedAt.ShouldBeNull();
        sut.Items.ShouldBeEmpty();
    }
}
