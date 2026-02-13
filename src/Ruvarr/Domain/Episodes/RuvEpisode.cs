namespace Ruvarr.Domain.Episodes;

internal sealed class RuvEpisode
{
    private RuvEpisode()
    {
    }

    public required string RuvId { get; init; }

    public required Uri Uri { get; init; }

    public required string Title { get; init; }

    public int? SeasonNumber { get; private set; }

    public int? EpisodeNumber { get; private set; }

    public DateTime? Downloaded { get; private set; }

    public static RuvEpisode Create(string id, Uri uri, string title)
    {
        return new RuvEpisode()
        {
            RuvId = id,
            Uri = uri,
            Title = title,
        };
    }

    public void Match(int season, int episode)
    {
        SeasonNumber = season;
        EpisodeNumber = episode;
    }

    public void Download()
    {
        Downloaded = DateTime.UtcNow;
    }
}