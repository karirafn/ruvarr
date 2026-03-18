using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;

internal sealed class GetTvdbSeriesEpisodesHandler(ITvdbClient tvdbClient, IMemoryCache cache)
    : IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>
{
    public async Task<IReadOnlyList<TvdbSeriesEpisode>> Handle(GetTvdbSeriesEpisodesQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"tvdb-series-episodes-{request.TvdbSeriesId}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<TvdbSeriesEpisode>? cached))
        {
            return cached!;
        }

        SeriesData? seriesData = await tvdbClient.GetSeriesAsync(request.TvdbSeriesId, cancellationToken);

        if (seriesData is null)
        {
            return [];
        }

        List<TvdbSeriesEpisode> episodes = [.. seriesData.Episodes
            .Where(e => e.SeasonNumber > 0)
            .Select(e => new TvdbSeriesEpisode(e.Id, e.Name, e.SeasonNumber, e.Number))
            .OrderBy(e => e.SeasonNumber)
            .ThenBy(e => e.EpisodeNumber)];

        cache.Set(cacheKey, episodes, TimeSpan.FromMinutes(30));

        return episodes;
    }
}
