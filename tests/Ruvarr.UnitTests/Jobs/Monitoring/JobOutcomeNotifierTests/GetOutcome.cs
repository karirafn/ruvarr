using Ruvarr.Jobs.Monitoring;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.Monitoring.JobOutcomeNotifierTests;

public sealed class GetOutcome
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly FixedTimeProvider FixedTime = new(FixedNow);

    [Fact]
    public void WhenNeverRun_ReturnsNull()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);

        // Act
        JobOutcome? outcome = sut.GetOutcome("SomeJob");

        // Assert
        outcome.ShouldBeNull();
    }

    [Fact]
    public void WhenRecordFailureCalledOnce_ReturnsOutcomeWithConsecutiveFailuresOne()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);

        // Act
        sut.RecordFailure("SomeJob", "something went wrong");

        // Assert
        JobOutcome outcome = sut.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(1),
            () => outcome.LastFailureAt.ShouldBe(FixedNow),
            () => outcome.LastError.ShouldBe("something went wrong"));
    }

    [Fact]
    public void WhenRecordFailureCalledMultipleTimes_ConsecutiveFailuresAccumulates()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);

        // Act
        sut.RecordFailure("SomeJob", "error 1");
        sut.RecordFailure("SomeJob", "error 2");
        sut.RecordFailure("SomeJob", "error 3");

        // Assert
        JobOutcome outcome = sut.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ConsecutiveFailures.ShouldBe(3);
    }

    [Fact]
    public void WhenDistinctJobNamesFail_TrackedIndependently()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);

        // Act
        sut.RecordFailure("JobA", "error a");
        sut.RecordFailure("JobA", "error a again");
        sut.RecordFailure("JobB", "error b");

        // Assert
        JobOutcome outcomeA = sut.GetOutcome("JobA").ShouldNotBeNull();
        JobOutcome outcomeB = sut.GetOutcome("JobB").ShouldNotBeNull();

        outcomeA.ConsecutiveFailures.ShouldBe(2);
        outcomeB.ConsecutiveFailures.ShouldBe(1);
    }

    [Fact]
    public void WhenRecordSuccessCalledAfterFailures_ResetsConsecutiveFailuresAndClearsError()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);
        sut.RecordFailure("SomeJob", "error 1");
        sut.RecordFailure("SomeJob", "error 2");

        // Act
        sut.RecordSuccess("SomeJob");

        // Assert
        JobOutcome outcome = sut.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(0),
            () => outcome.LastSuccessAt.ShouldBe(FixedNow),
            () => outcome.LastError.ShouldBeNull());
    }

    [Fact]
    public void WhenErrorIsMultiLine_StoredAsSingleLine()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);
        string multiLineError = "first line\nsecond line\rthird line";

        // Act
        sut.RecordFailure("SomeJob", multiLineError);

        // Assert
        JobOutcome outcome = sut.GetOutcome("SomeJob").ShouldNotBeNull();
        string lastError = outcome.LastError.ShouldNotBeNull();
        lastError.ShouldNotContain('\n');
        lastError.ShouldNotContain('\r');
    }

    [Fact]
    public void WhenErrorExceedsMaxLength_StoredTruncated()
    {
        // Arrange
        JobOutcomeNotifier sut = new(FixedTime);
        string longError = new('x', 300);

        // Act
        sut.RecordFailure("SomeJob", longError);

        // Assert
        JobOutcome outcome = sut.GetOutcome("SomeJob").ShouldNotBeNull();
        string lastError = outcome.LastError.ShouldNotBeNull();
        lastError.Length.ShouldBe(200);
    }
}
