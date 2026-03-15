using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.Programs;

public sealed class TvdbSeriesLookupNotifier : QueueNotifier<TvdbSeriesLookupQueueItemSummary>
{
    public bool TryDequeue(out int ruvId) => TryReadNext(out ruvId);

    protected override TvdbSeriesLookupQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, TvdbSeriesLookupStatus.Pending);

    protected override TvdbSeriesLookupQueueItemSummary WithProcessingStatus(TvdbSeriesLookupQueueItemSummary item) =>
        item with { Status = TvdbSeriesLookupStatus.Processing };
}