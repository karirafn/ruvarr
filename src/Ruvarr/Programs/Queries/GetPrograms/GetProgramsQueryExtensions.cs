using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetPrograms;

internal static class GetProgramsQueryExtensions
{
    public static IQueryable<RuvProgram> ApplyFilters(this IQueryable<RuvProgram> query, GetProgramsQuery request)
    {
        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(x => x.Channel == request.Channel);
        }

        if (request.IsUnmatched is true)
        {
            query = query.Where(x => x.Series == null && x.Movie == null);
        }
        else if (request.IsUnmatched is false)
        {
            query = query.Where(x => x.Series != null || x.Movie != null);
        }

        if (request.IsMonitored is true)
        {
            query = query.Where(x => x.IsMonitored);
        }
        else if (request.IsMonitored is false)
        {
            query = query.Where(x => !x.IsMonitored);
        }

        if (request.IsMissing is true)
        {
            query = query.Where(x => x.HasMissingEpisodes);
        }
        else if (request.IsMissing is false)
        {
            query = query.Where(x => !x.HasMissingEpisodes);
        }

        if (request.IsPendingLookup is true)
        {
            query = query.Where(x => x.NextLookup != null);
        }
        else if (request.IsPendingLookup is false)
        {
            query = query.Where(x => x.NextLookup == null);
        }

        if (request.HasForeignName is true)
        {
            query = query.Where(x => x.ForeignName != null);
        }
        else if (request.HasForeignName is false)
        {
            query = query.Where(x => x.ForeignName == null);
        }

        if (request.EpisodeMatch is EpisodeMatchStatus episodeMatch)
        {
            query = episodeMatch switch
            {
                EpisodeMatchStatus.FullyMatched => query.Where(x =>
                    x.Series != null &&
                    x.Episodes.Any() &&
                    x.Episodes.All(e => e.TvdbEpisodes.Any())),
                EpisodeMatchStatus.PartiallyMatched => query.Where(x =>
                    x.Series != null &&
                    x.Episodes.Any() &&
                    x.Episodes.Any(e => e.TvdbEpisodes.Any()) &&
                    !x.Episodes.All(e => e.TvdbEpisodes.Any())),
                _ => query.Where(x =>
                    x.Series == null ||
                    !x.Episodes.Any() ||
                    !x.Episodes.Any(e => e.TvdbEpisodes.Any())),
            };
        }

        return query;
    }
}
