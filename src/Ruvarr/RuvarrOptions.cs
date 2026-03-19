namespace Ruvarr;

internal sealed class RuvarrOptions
{
    public const string SectionName = "Ruvarr";

    public required string DownloadsRootDirectory { get; init; }

    public string? EpisodeDownloadDirectory { get; init; }

    public string? MovieDownloadDirectory { get; init; }

    public required IReadOnlyList<string> IgnoredChannels { get; init; } = [];
}
