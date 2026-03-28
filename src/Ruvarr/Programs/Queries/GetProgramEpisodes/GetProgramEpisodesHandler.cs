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
            x.TvdbEpisodes.Select(e =>
            {
                Uri? seriesUrl = x.SeriesSlug is { Length: > 0 }
                    ? new Uri($"https://www.thetvdb.com/series/{Uri.EscapeDataString(x.SeriesSlug)}")
                    : null;

                return new TvdbEpisodeSummary(
                    e.TvdbId, e.SeasonNumber, e.EpisodeNumber, e.IsMissing, e.HasIslTranslation,
                    seriesUrl is not null ? new Uri($"{seriesUrl}/episodes/{e.TvdbId}") : null);
            })
                .ToList(),
            x.FirstRun,
            string.IsNullOrEmpty(x.ProgramSlug)
                ? null
                : new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(x.ProgramSlug!)}/{x.ProgramRuvId}/{x.RuvId}"),
            x.Duration))
            .ToList();
    }
}