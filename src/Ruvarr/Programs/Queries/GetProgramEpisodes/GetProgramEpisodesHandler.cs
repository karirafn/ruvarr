using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetProgramEpisodes;

internal sealed class GetProgramEpisodesHandler(RuvarrDbContext dbContext) : IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>
{
    public Task<List<EpisodeSummary>> Handle(GetProgramEpisodesQuery request, CancellationToken cancellationToken)
        => dbContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .Where(x => x.Program.RuvId == request.ProgramRuvId)
            .OrderBy(x => x.TvdbEpisodes.Min(e => (int?)e.SeasonNumber))
            .ThenBy(x => x.TvdbEpisodes.Min(e => (int?)e.EpisodeNumber))
            .Select(x => new EpisodeSummary(
                x.Title,
                x.RuvId,
                x.Description,
                x.TvdbEpisodes.OrderBy(e => e.SeasonNumber).ThenBy(e => e.EpisodeNumber)
                    .Select(e => new EpisodeMatchSummary(e.TvdbId, e.SeasonNumber, e.EpisodeNumber, e.IsMissing))
                    .ToList(),
                x.FirstRun,
                string.IsNullOrEmpty(x.Program.Slug)
                    ? null
                    : new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(x.Program.Slug!)}/{x.Program.RuvId}/{x.RuvId}"),
                x.Duration))
            .ToListAsync(cancellationToken);
}