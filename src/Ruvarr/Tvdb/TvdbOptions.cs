namespace Ruvarr.Tvdb;

internal sealed class TvdbOptions
{
    internal const string SectionName = "Tvdb";

    public required string ApiKey { get; init; }
}