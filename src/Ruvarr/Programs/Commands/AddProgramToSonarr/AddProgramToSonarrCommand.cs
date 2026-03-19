namespace Ruvarr.Programs.Commands.AddProgramToSonarr;

public sealed record AddProgramToSonarrCommand(int RuvId, int QualityProfileId, string RootFolderPath);
