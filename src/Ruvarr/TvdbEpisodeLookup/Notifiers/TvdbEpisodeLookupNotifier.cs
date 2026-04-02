using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.TvdbEpisodeLookup.Notifiers;

public sealed class TvdbEpisodeLookupNotifier : QueueNotifier<TvdbEpisodeLookupQueueItemSummary>
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
            TvdbEpisodeLookupQueueItemSummary? processing = Items.FirstOrDefault(x => x.IsProcessing);
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

    protected override TvdbEpisodeLookupQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, TvdbEpisodeLookupStatus.Pending);

    protected override TvdbEpisodeLookupQueueItemSummary WithProcessingStatus(TvdbEpisodeLookupQueueItemSummary item) =>
        item with { Status = TvdbEpisodeLookupStatus.Processing };
}
