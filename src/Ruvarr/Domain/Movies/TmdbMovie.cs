namespace Ruvarr.Domain.Movies;

internal sealed class TmdbMovie
{
    private TmdbMovie()
    {
    }

    public required int TmdbId { get; init; }

    public required string TmdbName { get; init; }

    public static TmdbMovie Create(int id, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new()
        {
            TmdbId = id,
            TmdbName = name
        };
    }
}