namespace Ruvarr.Ruv.Domain;

internal sealed class RuvEpisode
{
    private RuvEpisode()
    {
    }

    public required RuvProgram Program { get; init; }

    public required string RuvId { get; init; }

    public required Uri Uri { get; init; }

    public required string Title { get; init; }

    public int? SeasonNumber { get; private set; }

    public int? EpisodeNumber { get; private set; }

    public int LookupCount { get; private set; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public DateTime? Downloaded { get; private set; }

    public static RuvEpisode Create(RuvProgram program, string id, Uri uri, string title)
    {
        return new RuvEpisode()
        {
            Program = program,
            RuvId = id,
            Uri = uri,
            Title = title,
        };
    }

    public void Match(int season, int episode)
    {
        Matched = DateTime.UtcNow;
        NextLookup = null;
        SeasonNumber = season;
        EpisodeNumber = episode;
    }

    public void Download()
    {
        Downloaded = DateTime.UtcNow;
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