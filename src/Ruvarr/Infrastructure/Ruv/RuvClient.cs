using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Infrastructure.Ruv;

internal sealed class RuvClient(ILogger<RuvClient> logger, HttpClient httpClient)
    : ApiClient(logger, httpClient), IRuvClient
{
    public async Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default)
    {
        Result<RuvFeaturedTv> result = await GetAsync<RuvFeaturedTv>("/api/programs/featured/tv", cancellationToken);
        return result.Match(v => (RuvFeaturedTv?)v, _ => default);
    }

    public async Task<RuvFeaturedTv?> GetKidsTvAsync(CancellationToken cancellationToken = default)
    {
        Result<RuvFeaturedTv> result = await GetAsync<RuvFeaturedTv>("/api/programs/featured/krakkaruv", cancellationToken);
        return result.Match(v => (RuvFeaturedTv?)v, _ => default);
    }

    public Task<Result<RuvTvProgram>> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default) =>
        GetAsync<RuvTvProgram>($"/api/programs/program/{seriesId}/all", cancellationToken);
}
