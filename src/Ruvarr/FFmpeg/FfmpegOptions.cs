namespace Ruvarr.FFmpeg;

internal sealed class FfmpegOptions
{
    internal const string SectionName = "Ffmpeg";

    public required string ExecutablePath { get; init; }
}