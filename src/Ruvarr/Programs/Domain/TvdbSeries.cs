namespace Ruvarr.Programs.Domain;

internal sealed class TvdbSeries
{
    private readonly List<RuvProgram> _programs = [];

    private TvdbSeries()
    {
    }

    public required string TvdbId { get; init; }

    public required string Name { get; init; }

    public string? Slug { get; private set; }

    public IReadOnlyList<RuvProgram> Programs => [.. _programs];

    internal void UpdateSlug(string? slug)
    {
        Slug = slug;
    }

    internal static TvdbSeries Create(string id, string name, string? slug = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new()
        {
            TvdbId = id,
            Name = name,
            Slug = slug
        };
    }
}