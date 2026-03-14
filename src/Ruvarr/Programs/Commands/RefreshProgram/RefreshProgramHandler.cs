using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.RefreshProgram;

internal sealed class RefreshProgramHandler(RuvarrDbContext dbContext, ProgramRefreshNotifier syncQueue, TvdbEpisodeLookupNotifier tvdbEpisodeLookupNotifier)
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

        syncQueue.Enqueue(program.RuvId, program.Name);

        if (program.Series is not null && program.HasMultipleEpisodes)
        {
            tvdbEpisodeLookupNotifier.Enqueue(program.RuvId, program.Name);
        }

        return RuvarrResult.Success;
    }
}
