using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.ClearManualSeasons;

internal sealed class ClearManualSeasonsHandler(RuvarrDbContext dbContext) : IRequestHandler<ClearManualSeasonsCommand>
{
    public async Task<RuvarrResult> Handle(ClearManualSeasonsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return ProgramErrors.ProgramNotFound;
        }

        program.ClearManualSeasons();
        // Matching falls back to the season parsed from the name, so links are re-checked against it.
        program.RequestRefresh();
        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
