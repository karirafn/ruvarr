using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.TvdbSeriesLookup.Notifiers;

public sealed class TvdbSeriesLookupNotifier : QueueNotifier<TvdbSeriesLookupQueueItemSummary>
{
    private readonly Lock _stateLock = new();
    private DateTimeOffset? _lastLookedUpAt;

    public DateTimeOffset? LastLookedUpAt
    {
        get { lock (_stateLock) { return _lastLookedUpAt; } }
    }

    public string? CurrentProgram
    {
        get
        {
            TvdbSeriesLookupQueueItemSummary? processing = Items.FirstOrDefault(x => x.IsProcessing);
            return processing?.ProgramName;
        }
    }

    public bool TryDequeue(out int ruvId) => TryReadNext(out ruvId);

    public new void MarkComplete(int ruvId)
    {
        lock (_stateLock)
        {
            base.MarkComplete(ruvId);
            _lastLookedUpAt = DateTimeOffset.UtcNow;
        }
    }

    protected override TvdbSeriesLookupQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, TvdbSeriesLookupStatus.Pending);

    protected override TvdbSeriesLookupQueueItemSummary WithProcessingStatus(TvdbSeriesLookupQueueItemSummary item) =>
        item with { Status = TvdbSeriesLookupStatus.Processing };
}
