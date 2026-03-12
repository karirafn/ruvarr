namespace Ruvarr.Contracts;

public sealed record ProgramSummary(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    string? SeriesName,
    IReadOnlyList<EpisodeSummary> Episodes);
