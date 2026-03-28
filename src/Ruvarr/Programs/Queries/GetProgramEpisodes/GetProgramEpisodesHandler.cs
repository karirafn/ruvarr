using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetProgramEpisodes;

internal sealed class GetProgramEpisodesHandler(RuvarrDbContext dbContext) : IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>
{
    public async Task<List<EpisodeSummary>> Handle(GetProgramEpisodesQuery request, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .Where(x => x.Program.RuvId == request.ProgramRuvId)
            .OrderBy(x => x.TvdbEpisodes.Min(e => (int?)e.SeasonNumber))
            .ThenBy(x => x.TvdbEpisodes.Min(e => (int?)e.EpisodeNumber))
            .Select(x => new
            {
                x.Title,
                x.RuvId,
                x.Description,
                TvdbEpisodes = x.TvdbEpisodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                    .Select(e => new { e.TvdbId, e.SeasonNumber, e.EpisodeNumber, e.IsMissing, e.HasIslTranslation })
                    .ToList(),
                x.FirstRun,
                ProgramSlug = x.Program.Slug,
                ProgramRuvId = x.Program.RuvId,
                SeriesSlug = x.Program.Series!.Slug,
                x.Duration
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new EpisodeSummary(
            x.Title,
            x.RuvId,
            x.Description,
            x.TvdbEpisodes.Select(e => new TvdbEpisodeSummary(
                e.TvdbId, e.SeasonNumber, e.EpisodeNumber, e.IsMissing, e.HasIslTranslation,
                string.IsNullOrEmpty(x.SeriesSlug)
                    ? null
                    : new Uri($"https://thetvdb.com/series/{x.SeriesSlug}/episodes/{e.TvdbId}")))
                .ToList(),
            x.FirstRun,
            string.IsNullOrEmpty(x.ProgramSlug)
                ? null
                : new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(x.ProgramSlug!)}/{x.ProgramRuvId}/{x.RuvId}"),
            x.Duration))
            .ToList();
    }
}