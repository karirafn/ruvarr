
namespace Ruvarr.Infrastructure.FFmpeg;

internal interface IFfmpegService
{
    Task DownloadAsync(Uri uri, string filepath, string title, IProgress<FfmpegProgressData>? progress = null, CancellationToken cancellationToken = default);

    Task<TimeSpan?> DetectTrimPointAsync(string filepath, CancellationToken cancellationToken = default);

    Task TrimStartAsync(string filepath, TimeSpan trimPoint, CancellationToken cancellationToken = default);
}