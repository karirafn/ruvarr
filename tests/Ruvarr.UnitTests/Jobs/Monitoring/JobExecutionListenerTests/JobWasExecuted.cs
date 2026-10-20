using NSubstitute;

using Quartz;

using Ruvarr.Jobs.Monitoring;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.Monitoring.JobExecutionListenerTests;

public sealed class JobWasExecuted
{
    private static readonly DateTimeOffset FixedNow = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly JobOutcomeNotifier _notifier = new(new FixedTimeProvider(FixedNow));
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly IJobDetail _jobDetail = Substitute.For<IJobDetail>();

    public JobWasExecuted()
    {
        _jobDetail.Key.Returns(new JobKey("SomeJob"));
        _context.JobDetail.Returns(_jobDetail);
    }

    private JobExecutionListener CreateSut() => new(_notifier);

    [Fact]
    public async Task WhenJobExceptionIsNull_RecordsSuccess()
    {
        // Arrange
        JobExecutionListener sut = CreateSut();

        // Act
        await sut.JobWasExecuted(_context, jobException: null, CancellationToken.None);

        // Assert
        JobOutcome outcome = _notifier.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(0),
            () => outcome.LastSuccessAt.ShouldBe(FixedNow),
            () => outcome.LastError.ShouldBeNull());
    }

    [Fact]
    public async Task WhenJobExceptionIsPresent_RecordsFailureWithMessage()
    {
        // Arrange
        JobExecutionListener sut = CreateSut();
        InvalidOperationException inner = new("inner failure message");
        JobExecutionException jobException = new(inner);

        // Act
        await sut.JobWasExecuted(_context, jobException, CancellationToken.None);

        // Assert
        JobOutcome outcome = _notifier.GetOutcome("SomeJob").ShouldNotBeNull();
        outcome.ShouldSatisfyAllConditions(
            () => outcome.ConsecutiveFailures.ShouldBe(1),
            () => outcome.LastFailureAt.ShouldBe(FixedNow),
            () => outcome.LastError.ShouldBe(jobException.Message));
    }

    [Fact]
    public async Task WhenJobExceptionContainsOperationCanceledException_RecordsNothing()
    {
        // Arrange
        JobExecutionListener sut = CreateSut();
        OperationCanceledException cancellation = new("job was cancelled");
        JobExecutionException jobException = new(cancellation);

        // Act
        await sut.JobWasExecuted(_context, jobException, CancellationToken.None);

        // Assert
        _notifier.GetOutcome("SomeJob").ShouldBeNull();
    }
}
