namespace Ruvarr.FFmpeg;

internal sealed class FfmpegOptions
{
    public required string ExecutablePath { get; init; }

    public required string OutputFolder { get; init; }
}