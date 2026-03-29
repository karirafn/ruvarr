using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.ProgramRefreshQueue.Notifiers;

public sealed class ProgramRefreshNotifier : QueueNotifier<ProgramRefreshQueueItemSummary>
{
    private readonly Lock _batchLock = new();
    private int _completedCount;
    private DateTimeOffset? _batchStartedAt;
    private DateTimeOffset? _lastCompletedAt;
    private TimeSpan? _lastRunDuration;
    private int? _lastRunTotal;

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
                _batchStartedAt = DateTimeOffset.UtcNow;
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
                _lastCompletedAt = DateTimeOffset.UtcNow;
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

    public IEnumerable<int> DequeueAll()
    {
        while (TryReadNext(out int ruvId))
        {
            yield return ruvId;
        }
    }

    protected override ProgramRefreshQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, ProgramRefreshStatus.Pending);

    protected override ProgramRefreshQueueItemSummary WithProcessingStatus(ProgramRefreshQueueItemSummary item) =>
        item with { Status = ProgramRefreshStatus.Processing };
}
