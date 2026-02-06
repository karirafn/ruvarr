using Ruvarr.Ruv.Models;

namespace Ruvarr;

public interface IRuvClient
{
    Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default);

    Task<RuvTvProgram?> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default);

    Task DownloadEpisodeAsync(RuvTvProgram program, RuvEpisode episode);
}