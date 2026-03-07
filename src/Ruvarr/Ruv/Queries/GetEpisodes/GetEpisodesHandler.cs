using Microsoft.EntityFrameworkCore;

using Ruvarr.Ruv.Domain;

namespace Ruvarr.Ruv.Queries.GetEpisodes;

public sealed class GetEpisodesHandler(RuvarrDbContext dbContext)
{
    public Task<List<EpisodeSummary>> Handle(string? programName, bool? isMatched = null, CancellationToken cancellationToken = default)
    {
        IQueryable<RuvEpisode> query = dbContext.Set<RuvEpisode>();

        if (!string.IsNullOrWhiteSpace(programName))
        {
            query = query.Where(x => x.Program.Name == programName);
        }

        if (isMatched is not null)
        {
            query = query.Where(x => (x.TvdbId == null) != isMatched);
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