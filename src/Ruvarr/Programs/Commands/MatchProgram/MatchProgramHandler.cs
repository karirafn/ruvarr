using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.MatchProgram;

internal sealed class MatchProgramHandler(
    RuvarrDbContext dbContext,
    ITvdbClient tvdb) : IRequestHandler<MatchProgramCommand>
{
    public async Task<RuvarrResult> Handle(MatchProgramCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TvdbSeriesId is < 1 or > 10_000_000)
        {
            return ProgramErrors.TvdbSeriesIdOutOfRange;
        }

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return ProgramErrors.ProgramNotFound;
        }

        SeriesData? seriesData = await tvdb.GetSeriesAsync(command.TvdbSeriesId, cancellationToken);
        if (seriesData is null)
        {
            return TvdbErrors.SeriesNotFound;
        }

        TvdbSeries? entity = await dbContext
            .Set<TvdbSeries>()
            .Where(x => x.TvdbId == command.TvdbSeriesId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? TvdbSeries.Create(command.TvdbSeriesId, seriesData.Series.Name, seriesData.Series.Slug);

        entity.UpdateSlug(seriesData.Series.Slug);

        program.MatchTvdb(entity);

        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
