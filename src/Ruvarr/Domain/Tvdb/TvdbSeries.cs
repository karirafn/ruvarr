using Ruvarr.Domain.Ruv;

namespace Ruvarr.Domain.Tvdb;

internal sealed class TvdbSeries
{
    private readonly List<RuvProgram> _programs = [];

    private TvdbSeries()
    {
    }

    public required string TvdbId { get; init; }

    public required string Type { get; init; }

    public required string Name { get; init; }

    public IReadOnlyList<RuvProgram> Programs => [.. _programs];

    internal static TvdbSeries Create(string id, string type, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new()
        {
            TvdbId = id,
            Type = type,
            Name = name
        };
    }
}