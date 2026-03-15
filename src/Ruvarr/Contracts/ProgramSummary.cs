namespace Ruvarr.Contracts;

public sealed record ProgramSummary(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    bool IsMonitored,
    bool HasMissingEpisodes,
    string? SeriesName,
    Uri? TvdbUrl);