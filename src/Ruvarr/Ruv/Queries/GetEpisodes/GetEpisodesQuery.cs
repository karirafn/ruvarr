namespace Ruvarr.Ruv.Queries.GetEpisodes;

public sealed record class GetEpisodesQuery(
    string? ProgramName,
    bool? IsProgramMonitored,
    bool? IsProgramMatched,
    bool? IsEpisodeMatched,
    bool? IsEpisodeMissing);