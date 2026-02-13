using System.Net.Http.Json;

using Ruvarr.FFmpeg;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Ruv;

internal sealed class RuvClient(HttpClient client, IFfmpegService ffmpegService) : IRuvClient
{
    public async Task<RuvFeaturedTv?> GetFeaturedTv(CancellationToken cancellationToken = default)
    {
        RuvFeaturedTv? response = await client.GetFromJsonAsync<RuvFeaturedTv>("/api/programs/featured/tv", cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    public async Task<RuvFeaturedTv?> GetKidsTvAsync(CancellationToken cancellationToken = default)
    {
        RuvFeaturedTv? response = await client.GetFromJsonAsync<RuvFeaturedTv>("/api/programs/featured/krakkaruv", cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    public async Task<RuvTvProgram?> GetProgramAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        RuvTvProgram? response = await client.GetFromJsonAsync<RuvTvProgram>($"/api/programs/program/{seriesId}/all", cancellationToken)
            .ConfigureAwait(false);

        return response;
    }

    public Task DownloadEpisodeAsync(RuvTvProgram program, RuvTvEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(episode);

        string filename = $"{program.Title} - {episode.Title}.mp4";
        return ffmpegService.DownloadAsync(episode.File, filename, episode.Title);
    }
}