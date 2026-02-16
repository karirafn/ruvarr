using System.Text.RegularExpressions;

using Ruvarr.Extensions;

namespace Ruvarr.Ruv.Domain;

internal sealed partial class RuvEpisode
{
    private RuvEpisode()
    {
    }

    public required RuvProgram Program { get; init; }

    public required string RuvId { get; init; }

    public required Uri Uri { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required DateTime FirstRun { get; init; }

    public int? TvdbId { get; private set; }

    public int? SeasonNumber { get; private set; }

    public int? EpisodeNumber { get; private set; }

    public int LookupCount { get; private set; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public DateTime? Downloaded { get; private set; }

    public static RuvEpisode Create(RuvProgram program, string id, Uri uri, string title, string description, DateTime firstRun)
    {
        return new RuvEpisode()
        {
            Program = program,
            RuvId = id,
            Uri = uri,
            Title = title,
            Description = description,
            FirstRun = firstRun,
        };
    }

    public void Match(int tvdbId, int season, int episode)
    {
        Matched = DateTime.UtcNow;
        NextLookup = null;
        TvdbId = tvdbId;
        SeasonNumber = season;
        EpisodeNumber = episode;
    }

    public void MarkDownloaded()
    {
        Downloaded = DateTime.UtcNow;
    }

    public void ScheduleLookup()
    {
        if (TvdbId is not null)
        {
            return;
        }

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

    public bool IsMatch(string value)
    {
        if (Title.Equals(value, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string title = Title;
        string[] parts = title.Split(' ');

        if (parts.Length > 1 && NumberPrefixRegex().IsMatch(parts[0]))
        {
            title = string.Join(' ', parts[1..]);
        }

        return title.EqualsSanitized(value);
    }

    [GeneratedRegex(@"^\d+\.$")]
    private static partial Regex NumberPrefixRegex();
}