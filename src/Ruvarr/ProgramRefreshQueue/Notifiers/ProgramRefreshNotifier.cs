using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.ProgramRefreshQueue.Notifiers;

public sealed class ProgramRefreshNotifier : QueueNotifier<ProgramRefreshQueueItemSummary>
{
    private readonly TimeProvider _timeProvider;
    private readonly Lock _batchLock = new();
    private int _completedCount;
    private DateTimeOffset? _batchStartedAt;
    private DateTimeOffset? _lastCompletedAt;
    private TimeSpan? _lastRunDuration;
    private int? _lastRunTotal;
    private DateTimeOffset? _lastEnqueuedAt;
    private int? _lastEnqueuedCount;

    public ProgramRefreshNotifier(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        StartedAt = _timeProvider.GetUtcNow();
    }

    public DateTimeOffset StartedAt { get; }

    public int CompletedCount
    {
        get { lock (_batchLock) { return _completedCount; } }
    }

    public DateTimeOffset? BatchStartedAt
    {
        get { lock (_batchLock) { return _batchStartedAt; } }
    }

    public DateTimeOffset? LastCompletedAt
    {
        get { lock (_batchLock) { return _lastCompletedAt; } }
    }

    public TimeSpan? LastRunDuration
    {
        get { lock (_batchLock) { return _lastRunDuration; } }
    }

    public int? LastRunTotal
    {
        get { lock (_batchLock) { return _lastRunTotal; } }
    }

    public DateTimeOffset? LastEnqueuedAt
    {
        get { lock (_batchLock) { return _lastEnqueuedAt; } }
    }

    public int? LastEnqueuedCount
    {
        get { lock (_batchLock) { return _lastEnqueuedCount; } }
    }

    public void RecordEnqueueBatch(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        lock (_batchLock)
        {
            _lastEnqueuedAt = _timeProvider.GetUtcNow();
            _lastEnqueuedCount = count;
        }
    }

    public string? CurrentProgram
    {
        get
        {
            ProgramRefreshQueueItemSummary? processing = Items.FirstOrDefault(x => x.IsProcessing);
            return processing?.ProgramName;
        }
    }

    public new void Enqueue(int ruvId, string programName)
    {
        lock (_batchLock)
        {
            bool wasEmpty = Items.Count == 0 && _completedCount == 0;
            base.Enqueue(ruvId, programName);
            if (wasEmpty && Items.Count > 0)
            {
                _batchStartedAt = _timeProvider.GetUtcNow();
            }
        }
    }

    public new void MarkComplete(int ruvId)
    {
        lock (_batchLock)
        {
            base.MarkComplete(ruvId);
            _completedCount++;

            if (Items.Count == 0)
            {
                _lastCompletedAt = _timeProvider.GetUtcNow();
                _lastRunTotal = _completedCount;
                if (_batchStartedAt is not null)
                {
                    _lastRunDuration = _lastCompletedAt - _batchStartedAt;
                }
                _completedCount = 0;
                _batchStartedAt = null;
            }
        }
    }

    public IQueueLease? TryLeaseNext() => TryLeaseNext(MarkComplete);

    protected override ProgramRefreshQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, ProgramRefreshStatus.Pending);

    protected override ProgramRefreshQueueItemSummary WithProcessingStatus(ProgramRefreshQueueItemSummary item) =>
        item with { Status = ProgramRefreshStatus.Processing };
}
