using Ruvarr.Internals.FFmpeg;
using Ruvarr.Internals.Ruv;
using Ruvarr.Internals.Tvdb;

namespace Ruvarr.Internals;

internal sealed class RuvarrOptions
{
    public const string SectionName = "Ruvarr";

    public required RuvOptions Ruv { get; init; }

    public required TvdbOptions Tvdb { get; init; }

    public required FfmpegOptions Ffmpeg { get; init; }
}