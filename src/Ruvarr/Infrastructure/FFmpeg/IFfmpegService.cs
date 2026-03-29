
namespace Ruvarr.Infrastructure.FFmpeg;

internal interface IFfmpegService
{
    Task DownloadAsync(Uri uri, string filepath, string title, IProgress<FfmpegProgressData>? progress = null);
}