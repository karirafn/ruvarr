namespace Ruvarr.Sonarr;

public sealed class SonarrOptions
{
    public const string SectionName = "Sonarr";

    public required Uri BaseAddress { get; init; }

    public required string ApiKey { get; init; }
}