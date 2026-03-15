using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Infrastructure.Ruv;

internal sealed class RuvClient(ILogger<RuvClient> logger, HttpClient httpClient)
    : ApiClient(logger, httpClient), IRuvClient
{
    public Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default) =>
        GetAsync<RuvFeaturedTv>("/api/programs/featured/tv", cancellationToken);

    public Task<RuvFeaturedTv?> GetKidsTvAsync(CancellationToken cancellationToken = default) =>
        GetAsync<RuvFeaturedTv>("/api/programs/featured/krakkaruv", cancellationToken);

    public Task<RuvTvProgram?> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default) =>
        GetAsync<RuvTvProgram>($"/api/programs/program/{seriesId}/all", cancellationToken);
}