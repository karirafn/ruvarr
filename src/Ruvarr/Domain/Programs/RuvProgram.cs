using Ruvarr.Domain.Movies;
using Ruvarr.Domain.Series;

namespace Ruvarr.Domain.Programs;

internal sealed class RuvProgram
{
    private RuvProgram()
    {
    }

    public required int RuvId { get; init; }

    public required string Channel { get; init; }

    public required string Name { get; init; }

    public required string? ForeignName { get; init; }

    public required bool HasMultipleEpisodes { get; init; }

    public required DateTime Created { get; init; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public int LookupCount { get; private set; }

    public TvdbSeries? Series { get; private set; }

    public TmdbMovie? Movie { get; private set; }

    public static RuvProgram Create(int id, string channgel, string name, string? foreignName, bool multipleEpisodes) => new()
    {
        RuvId = id,
        Channel = channgel,
        Name = name,
        ForeignName = foreignName,
        HasMultipleEpisodes = multipleEpisodes,
        Created = DateTime.UtcNow,
    };

    public void MatchTvdb(TvdbSeries series)
    {
        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        Series = series;
    }

    public void MatchTmdb(TmdbMovie movie)
    {
        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        Movie = movie;
    }

    public void ScheduleLookup()
    {
        LookupCount++;

        DateTime now = DateTime.UtcNow;
        NextLookup = LookupCount switch
        {
            1 => now.AddHours(1),
            2 => now.AddHours(2),
            3 => now.AddHours(4),
            4 => now.AddDays(1),
            _ => now.AddDays(7)
        };
    }
}