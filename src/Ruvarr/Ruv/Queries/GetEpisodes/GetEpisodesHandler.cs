using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Ruv.Domain;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.Models;

namespace Ruvarr.Ruv.Queries.GetEpisodes;

internal sealed class GetEpisodesHandler(RuvarrDbContext dbContext, ISonarrClient sonarr) : IRequestHandler<GetEpisodesQuery, List<EpisodeSummary>>
{
    public async Task<List<EpisodeSummary>> Handle(GetEpisodesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Series> series = request.IsProgramMonitored is not null || request.IsProgramMissingEpisodes is not null
            ? await sonarr.GetSeriesAsync(cancellationToken)
            : [];

        IReadOnlyCollection<MissingEpisode> missingEpisodes = request.IsEpisodeMissing is not null
            ? await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken)
            : [];

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
            HashSet<string> seriesIds = [.. series
                .Where(x => x.Monitored)
                .Select(x => x.TvdbId.ToString(CultureInfo.InvariantCulture))];
            query = query
                .Where(x => x.Program.Series != null)
                .Where(x => seriesIds.Contains(x.Program.Series!.TvdbId));
        }

        if (request.IsProgramMissingEpisodes is not null)
        {
            HashSet<string> seriesIds = [.. series
                .Where(x => x.Seasons.Where(s => s.SeasonNumber > 0).Any(s => (s.Statistics.PercentOfEpisodes < 1) == request.IsProgramMissingEpisodes))
                .Select(x => x.TvdbId.ToString(CultureInfo.InvariantCulture))];
            query = query
                .Where(x => x.Program.Series != null)
                .Where(x => seriesIds.Contains(x.Program.Series!.TvdbId));
        }

        if (request.IsEpisodeMissing is not null)
        {
            HashSet<int?> missingEpisodeIds = [.. missingEpisodes.Select(x => x.TvdbId)];
            query = query.Where(x => missingEpisodeIds.Contains(x.TvdbId));
        }

        if (request.IsProgramMatched is not null)
        {
            query = query.Where(x => (x.Program.Series == null) != request.IsProgramMatched);
        }

        if (request.IsEpisodeMatched is not null)
        {
            query = query.Where(x => (x.TvdbId == null) != request.IsEpisodeMatched);
        }

        return await query
            .OrderBy(x => x.Program.Name)
            .ThenBy(x => x.SeasonNumber)
            .ThenBy(x => x.EpisodeNumber)
            .Select(x => new EpisodeSummary(
                x.Program.Channel,
                x.Program.Name,
                x.Program.RuvId,
                x.Program.Series!.Name,
                x.Title,
                x.RuvId,
                x.Description,
                x.TvdbId,
                x.SeasonNumber,
                x.EpisodeNumber,
                x.FirstRun))
            .ToListAsync(cancellationToken);
    }
}