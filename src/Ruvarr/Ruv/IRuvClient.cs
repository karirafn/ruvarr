using Ruvarr.Ruv.Models;

namespace Ruvarr.Ruv;

public interface IRuvClient
{
    Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default);

    Task<RuvFeaturedTv?> GetKidsTvAsync(CancellationToken cancellationToken = default);

    Task<RuvTvProgram?> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default);
}