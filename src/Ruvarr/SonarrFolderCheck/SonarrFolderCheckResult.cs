namespace Ruvarr.SonarrFolderCheck;

public sealed record SonarrFolderCheckResult(SonarrFolderCheckStatus Status, string Folder, DateTimeOffset CheckedAt);
