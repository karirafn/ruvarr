using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;
using Ruvarr.Extensions;
using Ruvarr.Programs.Events;

namespace Ruvarr.Programs.Domain;

internal sealed partial class RuvEpisode
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private RuvEpisode()
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

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

    public bool IsMissing { get; private set; }

    public int? DurationSeconds { get; private set; }

    public DownloadQueueItem? DownloadQueueItem { get; private set; }

    public Uri? RuvUrl =>
        string.IsNullOrEmpty(Program.Slug)
            ? null
            : new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(Program.Slug!)}/{Program.RuvId}/{RuvId}");

    public static RuvEpisode Create(RuvProgram program, string id, Uri uri, string title, string description, DateTime firstRun, int? durationSeconds = null)
    {
        return new RuvEpisode()
        {
            Program = program,
            RuvId = id,
            Uri = uri,
            Title = title,
            Description = description,
            FirstRun = firstRun,
            DurationSeconds = durationSeconds,
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

    public string ToFilePath(string rootDirectory, string episodeSubdirectory, bool fileAlreadyExists)
    {
        string seriesOrProgramName = Program.Series?.Name ?? Program.Name;
        string directory = Path.Join(rootDirectory, episodeSubdirectory, seriesOrProgramName);

        string resolvedDirectory = Path.GetFullPath(directory);
        string resolvedRoot = Path.GetFullPath(rootDirectory);
        if (!resolvedDirectory.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && resolvedDirectory != resolvedRoot)
        {
            throw new InvalidOperationException($"Path traversal detected: '{resolvedDirectory}' is outside root '{resolvedRoot}'.");
        }

        string filename = ToFilename();

        if (fileAlreadyExists)
        {
            string extension = Path.GetExtension(filename);
            string stem = Path.GetFileNameWithoutExtension(filename);
            filename = $"{stem}.X{extension}";
        }

        return Path.Join(directory, filename);
    }

    public void Match(int tvdbId, int season, int episode, bool isMissing)
    {
        _domainEvents.Add(new EpisodeMatchedEvent(this));
        Matched = DateTime.UtcNow;
        NextLookup = null;
        TvdbId = tvdbId;
        SeasonNumber = season;
        EpisodeNumber = episode;
        SetMissing(isMissing);
    }

    public void SetMissing(bool isMissing)
    {
        if (isMissing && !IsMissing)
        {
            _domainEvents.Add(new EpisodeMissingEvent(this));
        }

        IsMissing = isMissing;
    }

    public void Download() => DownloadQueueItem = DownloadQueueItem.Create(this);

    public void ScheduleLookup()
    {
        if (TvdbId is not null)
        {
            return;
        }

        _domainEvents.Add(new EpisodeLookupScheduledEvent(this));
        LookupCount++;
        NextLookup = LookupSchedule.ComputeNextLookup(LookupCount);
    }

    public bool TryGetEpisodeNumber(out int number)
    {
        string[] parts = Title.Split(' ');

        if (parts.Length < 2 || !parts[0].Equals("þáttur", StringComparison.OrdinalIgnoreCase))
        {
            number = 0;
            return false;
        }

        return int.TryParse(parts[1], out number);
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

    [GeneratedRegex(@"^((\d+. (þ|Þ)áttur: )|((Þ|þ)áttur \d+: )|(\d+. (k|K)afli: )|(\d+.))")]
    private static partial Regex PrefixRegex();
}