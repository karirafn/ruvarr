using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetEpisodes;

internal sealed class GetEpisodesHandler(RuvarrDbContext dbContext) : IRequestHandler<GetEpisodesQuery, List<ProgramDetails>>
{
    public async Task<List<ProgramDetails>> Handle(GetEpisodesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<RuvEpisode> query = dbContext.Set<RuvEpisode>();

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(x => x.Program.Channel == request.Channel);
        }

        if (!string.IsNullOrWhiteSpace(request.ProgramName))
        {
            query = query.Where(x => x.Program.Name == request.ProgramName);
        }

        if (request.IsProgramMonitored is not null)
        {
            query = query.Where(x => x.Program.IsMonitored == request.IsProgramMonitored);
        }

        if (request.IsProgramMissingEpisodes is not null)
        {
            query = query.Where(x => x.Program.HasMissingEpisodes == request.IsProgramMissingEpisodes);
        }

        if (request.IsEpisodeMissing is not null)
        {
            query = query.Where(x => x.IsMissing == request.IsEpisodeMissing);
        }

        if (request.IsProgramMatched is not null)
        {
            query = query.Where(x => (x.Program.Series == null) != request.IsProgramMatched);
        }

        if (request.IsProgramPartiallyMatched is true)
        {
            query = query.Where(x =>
                x.Program.Series != null &&
                x.Program.Episodes.Any(e => e.TvdbId != null) &&
                x.Program.Episodes.Any(e => e.TvdbId == null));
        }

        if (request.IsEpisodeMatched is not null)
        {
            query = query.Where(x => (x.TvdbId == null) != request.IsEpisodeMatched);
        }

        var flat = await query
            .OrderBy(x => x.Program.Name)
            .ThenBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .Select(x => new
            {
                x.Program.Channel,
                ProgramName = x.Program.Name,
                ProgramRuvId = x.Program.RuvId,
                x.Program.IsMonitored,
                x.Program.HasMissingEpisodes,
                SeriesName = x.Program.Series!.Name,
                SeriesSlug = x.Program.Series!.Slug,
                EpisodeTitle = x.Title,
                EpisodeRuvId = x.RuvId,
                EpisodeDescription = x.Description,
                x.TvdbId,
                x.SeasonNumber,
                x.EpisodeNumber,
                x.FirstRun,
                x.IsMissing,
            })
            .ToListAsync(cancellationToken);

        IEnumerable<ProgramDetails> programs = flat
            .GroupBy(x => x.ProgramRuvId)
            .Select(g => new ProgramDetails(
                g.First().Channel,
                g.First().ProgramName,
                g.Key,
                g.First().IsMonitored,
                g.First().HasMissingEpisodes,
                g.First().SeriesName,
                g.First().SeriesSlug is { } slug ? new Uri($"https://www.thetvdb.com/series/{slug}") : null,
                [.. g.Select(e => new EpisodeSummary(
                    e.EpisodeTitle,
                    e.EpisodeRuvId,
                    e.EpisodeDescription,
                    e.TvdbId,
                    e.SeasonNumber,
                    e.EpisodeNumber,
                    e.FirstRun,
                    e.IsMissing))]))
            .OrderBy(p => p.ProgramName);

        return [.. programs];
    }
}