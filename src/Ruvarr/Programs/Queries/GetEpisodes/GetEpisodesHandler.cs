using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Queries.GetEpisodes;

internal sealed class GetEpisodesHandler(RuvarrDbContext dbContext, ISonarrClient sonarr) : IRequestHandler<GetEpisodesQuery, List<ProgramSummary>>
{
    public async Task<List<ProgramSummary>> Handle(GetEpisodesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);

        HashSet<int?> missingTvdbIds = [.. missingEpisodes.Select(x => x.TvdbId)];

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
            query = query.Where(x => missingTvdbIds.Contains(x.TvdbId) == request.IsEpisodeMissing);
        }

        if (request.IsProgramMatched is not null)
        {
            query = query.Where(x => (x.Program.Series == null) != request.IsProgramMatched);
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
                IsMonitored = x.Program.IsMonitored,
                HasMissingEpisodes = x.Program.HasMissingEpisodes,
                SeriesName = x.Program.Series!.Name,
                EpisodeTitle = x.Title,
                EpisodeRuvId = x.RuvId,
                EpisodeDescription = x.Description,
                x.TvdbId,
                x.SeasonNumber,
                x.EpisodeNumber,
                x.FirstRun,
            })
            .ToListAsync(cancellationToken);

        IEnumerable<ProgramSummary> programs = flat
            .GroupBy(x => x.ProgramRuvId)
            .Select(g => new ProgramSummary(
                g.First().Channel,
                g.First().ProgramName,
                g.Key,
                g.First().IsMonitored,
                g.First().HasMissingEpisodes,
                g.First().SeriesName,
                [.. g.Select(e => new EpisodeSummary(
                    e.EpisodeTitle,
                    e.EpisodeRuvId,
                    e.EpisodeDescription,
                    e.TvdbId,
                    e.SeasonNumber,
                    e.EpisodeNumber,
                    e.FirstRun,
                    missingTvdbIds.Contains(e.TvdbId)))]))
            .OrderBy(p => p.ProgramName);

        if (request.IsProgramMissingEpisodes == true)
        {
            programs = programs.Where(p => p.Episodes.Any(e => e.IsMissing));
        }

        return [.. programs];
    }
}