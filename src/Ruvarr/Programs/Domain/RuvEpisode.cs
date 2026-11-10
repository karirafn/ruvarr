using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Extensions;
using Ruvarr.Programs.Events;

namespace Ruvarr.Programs.Domain;

internal sealed partial class RuvEpisode
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<TvdbEpisode> _tvdbEpisodes = [];

    private RuvEpisode()
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public required RuvProgram Program { get; init; }

    public required string RuvId { get; init; }

    public required Uri Uri { get; init; }

    public string Title { get; private set; } = string.Empty;

    public required string Description { get; init; }

    public required DateTime FirstRun { get; init; }

    public required DateTime Created { get; init; }

    public IReadOnlyList<TvdbEpisode> TvdbEpisodes => _tvdbEpisodes.AsReadOnly();

    public int LookupCount { get; private set; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public TimeSpan Duration { get; private set; }

    private readonly List<DownloadQueueItem> _downloadQueueItems = [];
    public IReadOnlyList<DownloadQueueItem> DownloadQueueItems => _downloadQueueItems.AsReadOnly();

    public Uri? RuvUrl =>
        string.IsNullOrEmpty(Program.Slug)
            ? null
            : new Uri($"https://www.ruv.is/sjonvarp/spila/{Uri.EscapeDataString(Program.Slug)}/{Program.RuvId}/{RuvId}");

    public string? SeasonEpisodeLabel
    {
        get
        {
            if (_tvdbEpisodes.Count == 0)
            {
                return null;
            }

            List<TvdbEpisode> ordered = [.. _tvdbEpisodes
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)];

            StringBuilder builder = new();
            builder.AppendFormat(CultureInfo.InvariantCulture, "S{0:D2}", ordered[0].SeasonNumber);
            foreach (TvdbEpisode episode in ordered)
            {
                builder.AppendFormat(CultureInfo.InvariantCulture, "E{0:D2}", episode.EpisodeNumber);
            }

            return builder.ToString();
        }
    }

    public static RuvEpisode Create(RuvProgram program, string id, Uri uri, string title, string description, DateTime firstRun, TimeSpan duration)
    {
        return new RuvEpisode()
        {
            Program = program,
            RuvId = id,
            Uri = uri,
            Title = title,
            Description = description,
            FirstRun = firstRun,
            Duration = duration,
            Created = DateTime.UtcNow,
        };
    }

    public override string ToString()
    {
        string? label = SeasonEpisodeLabel;
        return label is null
            ? $"{Program.Name} - {Title}"
            : $"{Program.Name} {label} - {Title}";
    }

    public string ToFilename()
    {
        string namePart = string.IsNullOrWhiteSpace(Program.Series?.Name) ? Program.Name : Program.Series.Name;
        string episodePart = SeasonEpisodeLabel ?? Title;
        string filename = $"{namePart} {episodePart}"
            .Sanitized()
            .Replace(' ', '.');

        return $"{filename}-RUV.mp4";
    }

    public SonarrEpisodeStatus? SonarrStatus => CombineSonarrStatus([.. _tvdbEpisodes.Select(e => e.SonarrStatus)]);

    /// <summary>
    /// The Sonarr episode status of a RÚV episode from the statuses of its TVDB links: Downloaded only when
    /// every link is, otherwise Missing when any link is, otherwise NotTracked; <c>null</c> when unmatched.
    /// </summary>
    public static SonarrEpisodeStatus? CombineSonarrStatus(IReadOnlyCollection<SonarrEpisodeStatus> linkStatuses)
    {
        ArgumentNullException.ThrowIfNull(linkStatuses);

        if (linkStatuses.Count == 0)
        {
            return null;
        }

        if (linkStatuses.All(s => s == SonarrEpisodeStatus.Downloaded))
        {
            return SonarrEpisodeStatus.Downloaded;
        }

        return linkStatuses.Contains(SonarrEpisodeStatus.Missing)
            ? SonarrEpisodeStatus.Missing
            : SonarrEpisodeStatus.NotTracked;
    }

    public void Match(int tvdbId, int season, int episode, SonarrEpisodeStatuses sonarrStatuses, bool hasIslTranslation = false)
    {
        MatchMultiple([TvdbEpisode.Create(tvdbId, season, episode, hasIslTranslation)], sonarrStatuses);
    }

    public void MatchMultiple(IReadOnlyList<TvdbEpisode> tvdbEpisodes, SonarrEpisodeStatuses sonarrStatuses)
    {
        ArgumentNullException.ThrowIfNull(tvdbEpisodes);
        ArgumentNullException.ThrowIfNull(sonarrStatuses);

        SonarrEpisodeStatus? previousStatus = SonarrStatus;
        Dictionary<int, SonarrEpisodeStatus> storedStatuses = [];
        foreach (TvdbEpisode existing in _tvdbEpisodes)
        {
            storedStatuses.TryAdd(existing.TvdbId, existing.SonarrStatus);
        }

        foreach (TvdbEpisode tvdbEpisode in tvdbEpisodes)
        {
            tvdbEpisode.SonarrStatus = sonarrStatuses.StatusFor(
                tvdbEpisode.TvdbId,
                storedStatuses.GetValueOrDefault(tvdbEpisode.TvdbId, SonarrEpisodeStatus.NotTracked));
        }

        _domainEvents.Add(new EpisodeMatchedEvent(this));
        Matched = DateTime.UtcNow;
        NextLookup = null;
        _tvdbEpisodes.Clear();
        _tvdbEpisodes.AddRange(tvdbEpisodes);

        RaiseEpisodeMissingOnTransition(previousStatus);
    }

    public void UpdateSonarrStatus(IReadOnlyDictionary<int, SonarrEpisodeStatus> sonarrStatuses)
    {
        ArgumentNullException.ThrowIfNull(sonarrStatuses);

        SonarrEpisodeStatus? previousStatus = SonarrStatus;

        foreach (TvdbEpisode tvdbEpisode in _tvdbEpisodes)
        {
            tvdbEpisode.SonarrStatus = sonarrStatuses.GetValueOrDefault(tvdbEpisode.TvdbId, SonarrEpisodeStatus.NotTracked);
        }

        RaiseEpisodeMissingOnTransition(previousStatus);
    }

    private void RaiseEpisodeMissingOnTransition(SonarrEpisodeStatus? previousStatus)
    {
        if (previousStatus != SonarrEpisodeStatus.Missing && SonarrStatus == SonarrEpisodeStatus.Missing)
        {
            _domainEvents.Add(new EpisodeMissingEvent(this));
        }
    }

    public void RequestDownload() => _domainEvents.Add(new EpisodeDownloadRequestedEvent(this));

    public void ScheduleLookup()
    {
        if (_tvdbEpisodes.Count > 0)
        {
            return;
        }

        _domainEvents.Add(new EpisodeLookupScheduledEvent(this));
        LookupCount++;
        NextLookup = LookupSchedule.ComputeNextLookup(LookupCount);
    }

    public void UpdateTitle(string title)
    {
        Title = title;

        if (_tvdbEpisodes.Count == 0)
        {
            LookupCount = 0;
            NextLookup = null;
        }
    }

    public bool TryResolveSonarrEpisodeIds(
        IReadOnlyDictionary<int, int> tvdbIdToSonarrEpisodeId,
        out IReadOnlyList<int> sonarrEpisodeIds)
    {
        List<int> resolved = [];
        foreach (TvdbEpisode episode in _tvdbEpisodes)
        {
            if (!tvdbIdToSonarrEpisodeId.TryGetValue(episode.TvdbId, out int sonarrId))
            {
                sonarrEpisodeIds = [];
                return false;
            }

            resolved.Add(sonarrId);
        }

        sonarrEpisodeIds = resolved;
        return _tvdbEpisodes.Count > 0;
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

    public static bool IsPartTwo(string title)
    {
        return PartTwoSuffixRegex().IsMatch(title);
    }

    public static string GetBaseTitle(string title)
    {
        Match match = PartSuffixRegex().Match(title);
        return match.Success ? title[..match.Index] : title;
    }

    public static string ToPartOneTitle(string title)
    {
        return PartTwoSuffixRegex().Replace(title, m =>
        {
            string separator = m.Groups[1].Value;
            bool isUpperCase = char.IsUpper(m.Groups[2].Value[0]);
            string fyrri = isUpperCase ? "Fyrri" : "fyrri";
            return $"{separator}{fyrri} hluti";
        });
    }

    [GeneratedRegex(@"^((\d+. (þ|Þ)áttur: )|((Þ|þ)áttur \d+: )|(\d+. (k|K)afli: )|(\d+.))")]
    private static partial Regex PrefixRegex();

    [GeneratedRegex(@"(, | - )(s|S)(íðari|einni) hluti$")]
    private static partial Regex PartTwoSuffixRegex();

    [GeneratedRegex(@"(, | - )([fFsS])(yrri|íðari|einni) hluti$")]
    private static partial Regex PartSuffixRegex();
}