using Ruvarr.Jobs.Monitoring;
using Ruvarr.Testing.Time;

namespace Ruvarr.Testing.Builders;

internal sealed class JobOutcomeNotifierBuilder
{
    private static readonly DateTimeOffset DefaultFixedTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private TimeProvider _timeProvider = new FixedTimeProvider(DefaultFixedTime);
    private readonly List<Action<JobOutcomeNotifier>> _replays = [];

    public JobOutcomeNotifierBuilder WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    /// <summary>
    /// Loops <see cref="JobOutcomeNotifier.RecordFailure"/> the given number of times
    /// so the notifier has <paramref name="failureCount"/> consecutive failures for <paramref name="jobName"/>.
    /// </summary>
    public JobOutcomeNotifierBuilder WithFailingJob(string jobName, int failureCount, string error = "test error")
    {
        _replays.Add(notifier =>
        {
            for (int i = 0; i < failureCount; i++)
            {
                notifier.RecordFailure(jobName, error);
            }
        });

        return this;
    }

    /// <summary>
    /// Records one success for <paramref name="jobName"/>, resetting consecutive failures to 0.
    /// </summary>
    public JobOutcomeNotifierBuilder WithSucceededJob(string jobName)
    {
        _replays.Add(notifier => notifier.RecordSuccess(jobName));
        return this;
    }

    public JobOutcomeNotifier Build()
    {
        JobOutcomeNotifier notifier = new(_timeProvider);

        foreach (Action<JobOutcomeNotifier> replay in _replays)
        {
            replay(notifier);
        }

        return notifier;
    }
}
