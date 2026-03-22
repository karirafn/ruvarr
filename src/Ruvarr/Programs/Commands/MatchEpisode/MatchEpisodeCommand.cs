namespace Ruvarr.Programs.Commands.MatchEpisode;

public sealed record class MatchEpisodeCommand(string RuvId, IReadOnlyList<int> TvdbIds);