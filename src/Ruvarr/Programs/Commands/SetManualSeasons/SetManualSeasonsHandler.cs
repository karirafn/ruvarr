using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.SetManualSeasons;

internal sealed class SetManualSeasonsHandler(
    RuvarrDbContext dbContext,
    ITvdbClient tvdb) : IRequestHandler<SetManualSeasonsCommand>
{
    public async Task<RuvarrResult> Handle(SetManualSeasonsCommand command, CancellationToken cancellationToken)
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

        if (program.Series is null)
        {
            return ProgramErrors.ManualSeasonsRequireSeries;
        }

        SeriesData? seriesData = await tvdb.GetSeriesAsync(program.Series.TvdbId, cancellationToken);
        if (seriesData is null)
        {
            return TvdbErrors.SeriesNotFound;
        }

        RuvarrResult result = program.SetManualSeasons(command.Seasons, seriesData.Episodes.Select(x => x.SeasonNumber));
        if (result.IsFailure)
        {
            return result;
        }

        // The re-check unmatches Automatic links outside the set and the strategies re-match within it.
        program.RequestRefresh();
        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
