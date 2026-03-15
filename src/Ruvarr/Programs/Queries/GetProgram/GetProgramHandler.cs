using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetProgram;

internal sealed class GetProgramHandler(RuvarrDbContext dbContext) : IRequestHandler<GetProgramQuery, ProgramSummary?>
{
    public Task<ProgramSummary?> Handle(GetProgramQuery request, CancellationToken cancellationToken)
        => dbContext.Set<RuvProgram>()
            .Where(x => x.RuvId == request.ProgramRuvId)
            .Select(x => new
            {
                x.Channel,
                x.Name,
                x.RuvId,
                x.IsMonitored,
                x.HasMissingEpisodes,
                SeriesName = x.Series!.Name,
                SeriesSlug = x.Series!.Slug,
            })
            .Select(x => new ProgramSummary(
                x.Channel,
                x.Name,
                x.RuvId,
                x.IsMonitored,
                x.HasMissingEpisodes,
                x.SeriesName,
                x.SeriesSlug != null ? new Uri($"https://www.thetvdb.com/series/{Uri.EscapeDataString(x.SeriesSlug)}") : null))
            .FirstOrDefaultAsync(cancellationToken);
}