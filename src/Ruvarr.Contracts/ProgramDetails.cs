namespace Ruvarr.Contracts;

public sealed record ProgramDetails(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    bool IsMonitored,
    bool HasMissingEpisodes,
    string? SeriesName,
    IReadOnlyList<EpisodeSummary> Episodes);
