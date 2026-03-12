namespace Ruvarr.Infrastructure.Sonarr.Models;

public sealed record class Statistics(
    int EpisodeFileCount,
    int EpisodeCount,
    int TotalEpisodeCount,
    long SizeOnDisk,
    IReadOnlyList<string> ReleaseGroups,
    double PercentOfEpisodes);