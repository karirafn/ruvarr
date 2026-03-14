using System.Text.RegularExpressions;

namespace Ruvarr.Programs.Domain;

internal sealed partial class TvdbSeries
{
    private readonly List<RuvProgram> _programs = [];

    private TvdbSeries()
    {
    }

    public required string TvdbId { get; init; }

    public required string Name { get; init; }

    public string? Slug { get; private set; }

    public IReadOnlyList<RuvProgram> Programs => [.. _programs];

    [GeneratedRegex(@"^[a-z0-9\-]{1,128}$")]
    private static partial Regex SlugPattern();

    internal void UpdateSlug(string? slug)
    {
        if (slug is not null && !SlugPattern().IsMatch(slug))
            return;
        Slug = slug;
    }

    internal static TvdbSeries Create(string id, string name, string? slug = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Length, 256);

        return new()
        {
            TvdbId = id,
            Name = name,
            Slug = slug is not null && SlugPattern().IsMatch(slug) ? slug : null
        };
    }
}