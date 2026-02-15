
namespace Ruvarr.FFmpeg;

internal interface IFfmpegService
{
    Task DownloadAsync(Uri uri, string filepath, string title);
}