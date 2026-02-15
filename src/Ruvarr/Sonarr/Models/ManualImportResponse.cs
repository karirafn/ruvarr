namespace Ruvarr.Sonarr.Models;


internal sealed record class ManualImportResponse(IReadOnlyList<ManualImportFile> ManualImportFiles);