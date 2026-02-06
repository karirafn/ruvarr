namespace Ruvarr.Ruv;

internal sealed class RuvOptions
{
    internal const string SectionName = "Ruv";

    public required Uri BaseAddress { get; init; }
}
