namespace Ruvarr.Infrastructure.Sonarr.Models;


internal sealed record class ManualImportResponse(IReadOnlyList<ManualImportFile> ManualImportFiles);