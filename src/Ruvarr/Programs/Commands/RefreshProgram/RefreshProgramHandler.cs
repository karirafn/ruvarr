using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Programs.Commands.RefreshProgram;

internal sealed class RefreshProgramHandler(
    RuvarrDbContext dbContext,
    ProgramRefreshNotifier syncQueue,
    TvdbEpisodeLookupNotifier tvdbEpisodeLookupNotifier,
    TvdbSeriesLookupNotifier tvdbSeriesLookupNotifier,
    IDomainEventBroadcaster broadcaster)
    : IRequestHandler<RefreshProgramCommand>
{
    public async Task<RuvarrResult> Handle(RefreshProgramCommand command, CancellationToken cancellationToken)
    {
        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(p => p.Series)
            .Where(p => p.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return ProgramErrors.ProgramNotFound;
        }

        syncQueue.PriorityEnqueue(program.RuvId, program.Name);
        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());

        if (program.Series is null)
        {
            tvdbSeriesLookupNotifier.PriorityEnqueue(program.RuvId, program.Name);
            broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());
        }

        if (program.Series is not null && program.HasMultipleEpisodes)
        {
            tvdbEpisodeLookupNotifier.PriorityEnqueue(program.RuvId, program.Name);
            broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
        }

        return RuvarrResult.Success;
    }
}
