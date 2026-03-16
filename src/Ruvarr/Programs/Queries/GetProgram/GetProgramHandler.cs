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
                x.Slug,
                x.IsMonitored,
                x.HasMissingEpisodes,
                SeriesName = x.Series!.Name,
                SeriesSlug = x.Series!.Slug,
                SeriesTvdbId = (int?)x.Series!.TvdbId,
            })
            .Select(x => new ProgramSummary(
                x.Channel,
                x.Name,
                x.RuvId,
                x.IsMonitored,
                x.HasMissingEpisodes,
                x.SeriesName,
                x.SeriesSlug != null ? new Uri($"https://www.thetvdb.com/series/{Uri.EscapeDataString(x.SeriesSlug)}") : null,
                x.SeriesTvdbId,
                x.Slug != null ? new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(x.Slug)}/{x.RuvId}") : null))
            .FirstOrDefaultAsync(cancellationToken);
}