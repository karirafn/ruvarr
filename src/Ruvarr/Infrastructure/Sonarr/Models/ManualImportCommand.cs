namespace Ruvarr.Infrastructure.Sonarr.Models;

internal sealed record class ManualImportCommand(IEnumerable<ManualImportRequest> Files, string Mode = "Move")
{
    public string Name { get; } = "ManualImport";
}