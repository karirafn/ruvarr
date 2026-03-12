namespace Ruvarr.Infrastructure.Sonarr.Models;

public sealed record class Quality(
    int Id,
    string Name,
    string Source,
    int Resolution);