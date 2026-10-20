namespace Ruvarr.Jobs.Monitoring;

internal sealed class JobOutcomeNotifier(TimeProvider timeProvider)
{
    private const int MaxErrorLength = 200;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, JobOutcome> _outcomes = [];

    public void RecordFailure(string jobName, string error)
    {
        ArgumentNullException.ThrowIfNull(jobName);
        ArgumentNullException.ThrowIfNull(error);

        string truncated = Truncate(error);

        lock (_lock)
        {
            _outcomes.TryGetValue(jobName, out JobOutcome? existing);
            int count = (existing?.ConsecutiveFailures ?? 0) + 1;
            _outcomes[jobName] = new JobOutcome(
                ConsecutiveFailures: count,
                LastFailureAt: timeProvider.GetUtcNow(),
                LastSuccessAt: existing?.LastSuccessAt,
                LastError: truncated);
        }
    }

    public void RecordSuccess(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        lock (_lock)
        {
            _outcomes.TryGetValue(jobName, out JobOutcome? existing);
            _outcomes[jobName] = new JobOutcome(
                ConsecutiveFailures: 0,
                LastFailureAt: existing?.LastFailureAt,
                LastSuccessAt: timeProvider.GetUtcNow(),
                LastError: null);
        }
    }

    public JobOutcome? GetOutcome(string jobName)
    {
        ArgumentNullException.ThrowIfNull(jobName);

        lock (_lock)
        {
            return _outcomes.TryGetValue(jobName, out JobOutcome? outcome) ? outcome : null;
        }
    }

    private static string Truncate(string error)
    {
        string singleLine = error.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= MaxErrorLength
            ? singleLine
            : singleLine[..MaxErrorLength];
    }
}
