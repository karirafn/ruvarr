using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Ruvarr.Downloads.Domain;
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

    public DownloadQueueItem? DownloadQueueItem { get; private set; }

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

    public string ToFilename()
    {
        StringBuilder builder = new();

        if (string.IsNullOrWhiteSpace(Program.Series?.Name))
        {
            builder.Append(Program.Name);
        }
        else
        {
            builder.Append(Program.Series.Name);
        }

        builder.Append(' ');

        if (SeasonNumber is not null && EpisodeNumber is not null)
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "S{0:D2}E{1:D2}", SeasonNumber, EpisodeNumber);
        }
        else
        {
            builder.Append(Title);
        }

        string filename = builder.ToString()
            .Sanitized()
            .Replace(' ', '.');

        return $"{filename}-RUV.mp4";
    }

    public void Match(int tvdbId, int season, int episode)
    {
        Matched = DateTime.UtcNow;
        NextLookup = null;
        TvdbId = tvdbId;
        SeasonNumber = season;
        EpisodeNumber = episode;
    }

    public void Download() => DownloadQueueItem = DownloadQueueItem.Create(this);

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

        Match match = PrefixRegex().Match(title);

        if (match.Success)
        {
            title = title[match.Value.Length..].Trim();
        }

        return title.EqualsSanitized(value);
    }

    [GeneratedRegex(@"^(\d+. (þ|Þ)áttur: )|((Þ|þ)áttur \d+: )|(\d+. (k|K)afli: )|(\d+. )")]
    private static partial Regex PrefixRegex();
}