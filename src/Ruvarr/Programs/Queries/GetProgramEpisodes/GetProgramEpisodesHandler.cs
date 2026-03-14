using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetProgramEpisodes;

internal sealed class GetProgramEpisodesHandler(RuvarrDbContext dbContext) : IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>
{
    public Task<List<EpisodeSummary>> Handle(GetProgramEpisodesQuery request, CancellationToken cancellationToken)
        => dbContext.Set<RuvEpisode>()
            .Where(x => x.Program.RuvId == request.ProgramRuvId)
            .OrderBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .Select(x => new EpisodeSummary(
                x.Title,
                x.RuvId,
                x.Description,
                x.TvdbId,
                x.SeasonNumber,
                x.EpisodeNumber,
                x.FirstRun,
                x.IsMissing))
            .ToListAsync(cancellationToken);
}
