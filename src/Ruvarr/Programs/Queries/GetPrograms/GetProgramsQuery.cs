namespace Ruvarr.Programs.Queries.GetPrograms;

public sealed record GetProgramsQuery(
    string? Channel,
    bool? IsProgramMonitored,
    bool? IsProgramMissingEpisodes,
    bool? IsProgramMatched,
    bool? IsProgramPartiallyMatched);