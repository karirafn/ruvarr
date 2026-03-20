namespace Ruvarr.Programs.Queries.GetPrograms;

using Ruvarr.Contracts;

public sealed record GetProgramsQuery(
    string? Channel,
    bool? IsUnmatched,
    bool? IsMonitored,
    bool? IsMissing,
    bool? IsPendingLookup,
    bool? HasForeignName,
    EpisodeMatchStatus? EpisodeMatch);