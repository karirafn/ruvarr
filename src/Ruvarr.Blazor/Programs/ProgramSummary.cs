namespace Ruvarr.Blazor.Programs;

internal sealed record ProgramSummary(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    string? SeriesName,
    List<EpisodeSummary> Episodes);
