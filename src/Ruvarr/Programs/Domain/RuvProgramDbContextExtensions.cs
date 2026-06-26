using Microsoft.EntityFrameworkCore;

namespace Ruvarr.Programs.Domain;

internal static class RuvProgramDbContextExtensions
{
    public static async Task<RuvProgram?> FindRuvProgramAsync(
        this RuvarrDbContext dbContext,
        int ruvId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<RuvProgram>()
            .Include(x => x.Episodes)
                .ThenInclude(x => x.TvdbEpisodes)
            .FirstOrDefaultAsync(x => x.RuvId == ruvId, cancellationToken);
    }
}
