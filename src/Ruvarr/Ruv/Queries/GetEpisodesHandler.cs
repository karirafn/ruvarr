using Microsoft.EntityFrameworkCore;

using Ruvarr.Ruv.Domain;
using Ruvarr.Ruv.Queries.GetEpisodes;

namespace Ruvarr.Ruv.Queries;

public sealed class GetEpisodesHandler(RuvarrDbContext dbContext)
{
    public Task<List<EpisodeSummary>> Handle(string? programName, CancellationToken cancellationToken = default)
    {
        IQueryable<RuvEpisode> query = dbContext.Set<RuvEpisode>();

        if (!string.IsNullOrWhiteSpace(programName))
        {
            query = query.Where(x => x.Program.Name == programName);
        }

        return query
            .OrderBy(x => x.Program.Name)
            .ThenBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .Select(x => new EpisodeSummary(
                x.Program.Name,
                x.Program.Series!.Name,
                x.Title,
                x.RuvId,
                x.TvdbId,
                x.SeasonNumber,
                x.EpisodeNumber))
            .ToListAsync(cancellationToken);
    }
}