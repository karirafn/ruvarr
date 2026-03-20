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
        IQueryable<RuvProgram> query = dbContext
            .Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes);

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(x => x.Channel == request.Channel);
        }

        if (request.IsProgramMonitored is not null)
        {
            query = query.Where(x => x.IsMonitored == request.IsProgramMonitored);
        }

        if (request.IsProgramMissingEpisodes is not null)
        {
            query = query.Where(x => x.HasMissingEpisodes == request.IsProgramMissingEpisodes);
        }

        if (request.IsProgramMatched is not null)
        {
            query = query.Where(x => (x.Series == null) != request.IsProgramMatched);
        }

        if (request.IsProgramPartiallyMatched is true)
        {
            query = query.Where(x =>
                x.Series != null &&
                x.Episodes.Any(e => e.TvdbId != null) &&
                x.Episodes.Any(e => e.TvdbId == null));
        }

        if (request.IsEpisodeMatched is not null)
        {
            query = query.Where(x => x.Episodes.Any(e => (e.TvdbId == null) != request.IsEpisodeMatched));
        }

        IAsyncEnumerable<ProgramSummary> results = query
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
                SeriesSlug = x.Series!.Slug,
                SeriesTvdbId = (int?)x.Series!.TvdbId,
                HasSeries = x.Series != null,
                HasAnyEpisodes = x.Episodes.Any(),
                AllEpisodesMatched = x.Episodes.All(e => e.TvdbId != null),
                AnyEpisodeMatched = x.Episodes.Any(e => e.TvdbId != null),
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
                null));

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