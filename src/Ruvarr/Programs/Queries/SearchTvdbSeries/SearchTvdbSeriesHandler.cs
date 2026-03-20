using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Programs.Queries.SearchTvdbSeries;

internal sealed class SearchTvdbSeriesHandler(ITvdbClient tvdbClient, IMemoryCache cache)
    : IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>
{
    public async Task<IReadOnlyList<TvdbSeriesSuggestion>> Handle(SearchTvdbSeriesQuery request, CancellationToken cancellationToken)
    {
        string cacheKey = $"tvdb-search-series-{request.ProgramName}";

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<TvdbSeriesSuggestion>? cached))
        {
            return cached!;
        }

        SearchResponse response = await tvdbClient.SearchAsync(query: request.ProgramName, cancellationToken: cancellationToken);

        List<TvdbSeriesSuggestion> suggestions = [.. response.Data
            .Where(d => d.Type == "series")
            .Where(d => int.TryParse(d.TvdbId, out _))
            .Select(d => new TvdbSeriesSuggestion(int.Parse(d.TvdbId, System.Globalization.CultureInfo.InvariantCulture), d.Name, d.Year))
            .Take(20)];

        MemoryCacheEntryOptions cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            Size = 1
        };
        cache.Set(cacheKey, suggestions, cacheOptions);

        return suggestions;
    }
}
