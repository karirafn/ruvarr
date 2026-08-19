using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetPrograms;

internal sealed class GetProgramsHandler(RuvarrDbContext dbContext) : IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>
{
    public async IAsyncEnumerable<ProgramSummary> Handle(GetProgramsQuery request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IAsyncEnumerable<ProgramSummary> results = dbContext
            .Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .ApplyFilters(request)
            .IgnoreAutoIncludes()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Channel,
                x.Name,
                x.RuvId,
                x.Slug,
                x.IsMonitored,
                x.HasMissingEpisodes,
                SeriesName = x.Series!.Name,
                SeriesSlug = x.Series.Slug,
                SeriesTvdbId = (int?)x.Series.TvdbId,
                HasSeries = x.Series != null,
                EpisodeCount = x.Episodes.Count,
                HasAnyEpisodes = x.Episodes.Any(),
                AllEpisodesMatched = x.Episodes.All(e => e.TvdbEpisodes.Any()),
                AnyEpisodeMatched = x.Episodes.Any(e => e.TvdbEpisodes.Any()),
                HasMovie = x.Movie != null,
            })
            .AsAsyncEnumerable()
            .Select(x => new ProgramSummary(
                x.Channel,
                x.Name,
                x.RuvId,
                x.IsMonitored,
                x.HasMissingEpisodes,
                x.SeriesName,
                x.SeriesSlug is { } seriesSlug ? new Uri($"https://www.thetvdb.com/series/{Uri.EscapeDataString(seriesSlug)}") : null,
                x.SeriesTvdbId,
                x.Slug is { } programSlug ? new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(programSlug)}/{x.RuvId}") : null,
                DeriveEpisodeMatchStatus(x.HasSeries, x.HasAnyEpisodes, x.AllEpisodesMatched, x.AnyEpisodeMatched),
                null,
                x.HasMovie,
                null,
                x.EpisodeCount));

        await foreach (ProgramSummary summary in results.WithCancellation(cancellationToken))
        {
            yield return summary;
        }
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