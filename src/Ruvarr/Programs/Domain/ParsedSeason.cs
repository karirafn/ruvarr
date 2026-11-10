namespace Ruvarr.Programs.Domain;

/// <summary>The season a RÚV program's name gives, with the name's last word it was parsed from (e.g. "II" → 2).</summary>
internal sealed record ParsedSeason(string Token, int Number);
