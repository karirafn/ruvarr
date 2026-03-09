namespace Ruvarr.Ruv.Queries.GetEpisodes;

public sealed record class GetEpisodesQuery(
    string? Channel,
    string? ProgramName,
    bool? IsProgramMonitored,
    bool? IsProgramMissingEpisodes,
    bool? IsProgramMatched,
    bool? IsEpisodeMatched,
    bool? IsEpisodeMissing);