namespace Ruvarr.Programs.Commands.SetManualSeasons;

public sealed record SetManualSeasonsCommand(int RuvId, IReadOnlyList<int> Seasons);
