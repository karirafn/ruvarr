namespace Ruvarr.Sonarr.Models;

public sealed record class Revision(
    int Version,
    int Real,
    bool IsRepack);
