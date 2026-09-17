using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Ruv.Models;

namespace Ruvarr.Infrastructure.Ruv;

internal interface IRuvClient
{
    Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default);

    Task<RuvFeaturedTv?> GetKidsTvAsync(CancellationToken cancellationToken = default);

    Task<Result<RuvTvProgram>> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default);
}