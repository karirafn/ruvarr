using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.TvdbEpisodeLookup.Notifiers;

public sealed class TvdbEpisodeLookupNotifier : QueueNotifier<TvdbEpisodeLookupQueueItemSummary>
{
    public bool TryDequeue(out int ruvId) => TryReadNext(out ruvId);

    protected override TvdbEpisodeLookupQueueItemSummary CreatePending(int ruvId, string programName) =>
        new(ruvId, programName, TvdbEpisodeLookupStatus.Pending);

    protected override TvdbEpisodeLookupQueueItemSummary WithProcessingStatus(TvdbEpisodeLookupQueueItemSummary item) =>
        item with { Status = TvdbEpisodeLookupStatus.Processing };
}
