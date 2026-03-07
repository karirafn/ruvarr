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
        IQueryable<RuvEpisode> query = dbContext.Set<RuvEpisode>();

        if (!string.IsNullOrWhiteSpace(request.ProgramName))
        {
            query = query.Where(x => x.Program.Name == request.ProgramName);
        }

        if (request.IsProgramMonitored is not null)
        {
            IReadOnlyList<Series> series = await sonarr.GetSeriesAsync(cancellationToken);
            List<string> monitoredSeriesIds = [.. series
                .Where(x => x.Monitored)
                .Select(x => x.TvdbId.ToString(CultureInfo.InvariantCulture))];
#pragma warning disable CA1305 // Specify IFormatProvider
            query = query
                .Where(x => x.Program.Series != null)
                .Where(x => monitoredSeriesIds.Contains(x.Program.Series!.TvdbId));
#pragma warning restore CA1305 // Specify IFormatProvider
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