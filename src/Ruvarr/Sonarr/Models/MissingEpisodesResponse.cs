namespace Ruvarr.Sonarr.Models;

public sealed record class MissingEpisodesResponse(
    int Page,
    int PageSize,
    string SortKey,
    string SortDirection,
    int TotalRecords,
    IReadOnlyList<MissingEpisode> Records);