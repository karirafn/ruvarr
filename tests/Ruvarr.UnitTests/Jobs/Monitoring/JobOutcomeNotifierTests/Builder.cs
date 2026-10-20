using Ruvarr.Jobs.Monitoring;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.Monitoring.JobOutcomeNotifierTests;

public sealed class JobOutcomeNotifierBuilderTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void WithFailingJob_BuildsNotifierWithExpectedConsecutiveFailures()
    {
        // Arrange & Act
        JobOutcomeNotifier notifier = new JobOutcomeNotifierBuilder()
            .WithTimeProvider(new FixedTimeProvider(FixedNow))
            .WithFailingJob("SomeJob", failureCount: 3, error: "oops")
            .Build();

        // Assert
        JobOutcome outcome = notifier.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(3),
            () => outcome.LastError.ShouldBe("oops"),
            () => outcome.LastFailureAt.ShouldBe(FixedNow));
    }

    [Fact]
    public void WithSucceededJob_BuildsNotifierWithZeroConsecutiveFailures()
    {
        // Arrange & Act
        JobOutcomeNotifier notifier = new JobOutcomeNotifierBuilder()
            .WithTimeProvider(new FixedTimeProvider(FixedNow))
            .WithFailingJob("SomeJob", failureCount: 2, error: "prior error")
            .WithSucceededJob("SomeJob")
            .Build();

        // Assert
        JobOutcome outcome = notifier.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(0),
            () => outcome.LastSuccessAt.ShouldBe(FixedNow),
            () => outcome.LastError.ShouldBeNull());
    }

    [Fact]
    public void WithMultipleJobs_TracksEachJobIndependently()
    {
        // Arrange & Act
        JobOutcomeNotifier notifier = new JobOutcomeNotifierBuilder()
            .WithTimeProvider(new FixedTimeProvider(FixedNow))
            .WithFailingJob("JobA", failureCount: 2)
            .WithFailingJob("JobB", failureCount: 5)
            .Build();

        // Assert
        JobOutcome outcomeA = notifier.GetOutcome("JobA").ShouldNotBeNull();
        JobOutcome outcomeB = notifier.GetOutcome("JobB").ShouldNotBeNull();

        outcomeA.ConsecutiveFailures.ShouldBe(2);
        outcomeB.ConsecutiveFailures.ShouldBe(5);
    }
}
