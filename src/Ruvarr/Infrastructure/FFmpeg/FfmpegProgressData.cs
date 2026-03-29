namespace Ruvarr.Infrastructure.FFmpeg;

internal sealed record FfmpegProgressData(long BytesDownloaded, TimeSpan Time, long? EstimatedTotalBytes = null);
