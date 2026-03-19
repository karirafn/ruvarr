using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetProgram;

internal sealed class GetProgramHandler(RuvarrDbContext dbContext) : IRequestHandler<GetProgramQuery, ProgramSummary?>
{
    public async Task<ProgramSummary?> Handle(GetProgramQuery request, CancellationToken cancellationToken)
    {
        var projected = await dbContext.Set<RuvProgram>()
            .IgnoreAutoIncludes()
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
                HasSeries = x.Series != null,
                x.ImageUrl,
                HasAnyEpisodes = x.Episodes.Any(),
                AllEpisodesMatched = x.Episodes.All(e => e.TvdbId != null),
                AnyEpisodeMatched = x.Episodes.Any(e => e.TvdbId != null),
                HasMovie = x.Movie != null,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (projected is null)
        {
            return null;
        }

        EpisodeMatchStatus matchStatus = DeriveEpisodeMatchStatus(
            projected.HasSeries, projected.HasAnyEpisodes, projected.AllEpisodesMatched, projected.AnyEpisodeMatched);

        return new ProgramSummary(
            projected.Channel,
            projected.Name,
            projected.RuvId,
            projected.IsMonitored,
            projected.HasMissingEpisodes,
            projected.SeriesName,
            projected.SeriesSlug != null ? new Uri($"https://www.thetvdb.com/series/{Uri.EscapeDataString(projected.SeriesSlug)}") : null,
            projected.SeriesTvdbId,
            projected.Slug != null ? new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(projected.Slug)}/{projected.RuvId}") : null,
            matchStatus,
            projected.ImageUrl,
            projected.HasMovie);
    }

    private static EpisodeMatchStatus DeriveEpisodeMatchStatus(bool hasSeries, bool hasAnyEpisodes, bool allEpisodesMatched, bool anyEpisodeMatched)
    {
        if (!hasSeries || !hasAnyEpisodes || !anyEpisodeMatched)
        {
            return EpisodeMatchStatus.NoneMatched;
        }

        return allEpisodesMatched ? EpisodeMatchStatus.FullyMatched : EpisodeMatchStatus.PartiallyMatched;
    }
}