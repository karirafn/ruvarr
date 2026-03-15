using Ruvarr.Abstractions;
using Ruvarr.Contracts;

namespace Ruvarr.Programs;

public sealed class ProgramRefreshNotifier : QueueNotifier<ProgramRefreshQueueItemSummary>
{
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