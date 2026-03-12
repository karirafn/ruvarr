using Ruvarr.Abstractions;

namespace Ruvarr.Programs;

public static class ProgramErrors
{
    public const string ProgramNotFoundCode = "Programs.NotFound";
    public const string EpisodeNotFoundCode = "Episodes.NotFound";

    public static RuvarrError ProgramNotFound => new(
        ProgramNotFoundCode,
        "Program not found.");

    public static RuvarrError ProgramNotMatched => new(
        "Programs.NotMatched",
        "Program is not matched.");

    public static RuvarrError EpisodeNotFound => new(
        EpisodeNotFoundCode,
        "Episode not found.");

    public static RuvarrError ProgramEpisodeCountMismatch => new(
        "Programs.EpisodeCountMismatch",
        "Program episode count does not match TVDB episode count");

    public static RuvarrError UnparsableEpisodeTitle => new(
        "Episodes.UnparsableTitle",
        "Episode titles must be on start with 'Þáttur' followed by an integer for them to be parsable.");

    public static RuvarrError SeasonUndetermined => new(
        "Episodes.SeasonUndetermined",
        "TVDB has multiple seasons. Failed to determine which season the RÚV episodes are from.");
}