namespace Ruvarr.Programs.Queries.GetEpisodes;

public sealed record class GetEpisodesQuery(
    string? Channel,
    string? ProgramName,
    bool? IsProgramMonitored,
    bool? IsProgramMissingEpisodes,
    bool? IsProgramMatched,
    bool? IsProgramPartiallyMatched,
    bool? IsEpisodeMatched,
    bool? IsEpisodeMissing);