namespace Ruvarr.Programs.Queries.GetEpisodes;

public sealed record class ProgramSummary(
    string Channel,
    string ProgramName,
    int ProgramRuvId,
    string? SeriesName,
    IReadOnlyList<EpisodeSummary> Episodes);
