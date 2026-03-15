namespace Ruvarr.Infrastructure.Sonarr.Models;

public sealed record class Revision(
    int Version,
    int Real,
    bool IsRepack);