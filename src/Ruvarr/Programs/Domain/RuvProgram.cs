using System.Text.RegularExpressions;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Events;
using Ruvarr.RomanNumerals;

namespace Ruvarr.Programs.Domain;

internal sealed partial class RuvProgram
{
    private const int MaxDescriptionLength = 4096;
    private static readonly Regex SlugPattern = new(@"^[a-z0-9\-]{1,128}$", RegexOptions.Compiled);

    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly List<RuvEpisode> _episodes = [];

    private RuvProgram()
    {
    }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public required int RuvId { get; init; }

    public required string Channel { get; init; }

    public required string Name { get; init; }

    public required string? ForeignName { get; init; }

    public bool HasMultipleEpisodes { get; private set; }

    public required DateTime Created { get; init; }

    public DateTime? Matched { get; private set; }

    public DateTime? NextLookup { get; private set; }

    public int LookupCount { get; private set; }

    public bool IsMonitored { get; private set; }

    public bool HasMissingEpisodes { get; private set; }

    public string? Slug { get; private set; }

    public string? Description { get; private set; }

    public Uri? ImageUrl { get; private set; }

    public TvdbSeries? Series { get; private set; }

    public TmdbMovie? Movie { get; private set; }

    public IReadOnlyList<RuvEpisode> Episodes => _episodes.AsReadOnly();

    public int SeasonNumber
    {
        get
        {
            int season = ParseSeasonFromSuffix(Name);

            if (season > 0)
            {
                return season;
            }

            return ParseSeasonFromSuffix(ForeignName);
        }
    }

    private static int ParseSeasonFromSuffix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        string suffix = name.Split(' ')[^1];

        if (RomanNumeral.TryParse(suffix, out RomanNumeral? rn) && rn.Number > 0)
        {
            return rn.Number;
        }

        if (int.TryParse(suffix, out int n) && n > 0)
        {
            return n;
        }

        return 0;
    }

    public static RuvProgram Create(int id, string channel, string name, string? foreignName, bool multipleEpisodes, string? slug = null, Uri? imageUrl = null, string? description = null)
    {
        RuvProgram program = new()
        {
            RuvId = id,
            Channel = channel,
            Name = name,
            ForeignName = foreignName,
            HasMultipleEpisodes = multipleEpisodes,
            Created = DateTime.UtcNow,
            Slug = SanitizeSlug(slug),
            ImageUrl = imageUrl,
            Description = TruncateDescription(description),
        };

        program._domainEvents.Add(new ProgramCreatedEvent(program));

        return program;
    }

    public void UpdateSlug(string? slug) => Slug = SanitizeSlug(slug);

    public void UpdateImageUrl(Uri? imageUrl) => ImageUrl = imageUrl;

    public void UpdateDescription(string? description) => Description = TruncateDescription(description);

    private static string? TruncateDescription(string? description) =>
        description?.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description;

    private static string? SanitizeSlug(string? slug) =>
        slug is not null && SlugPattern.IsMatch(slug) ? slug : null;

    public void UpdateHasMultipleEpisodes(bool value)
    {
        if (HasMultipleEpisodes == value)
        {
            return;
        }

        HasMultipleEpisodes = value;
    }

    public void SetMonitoredStatus(bool monitored)
    {
        if (IsMonitored == monitored)
        {
            return;
        }

        IsMonitored = monitored;
        _domainEvents.Add(new ProgramMonitoredStatusChangedEvent(RuvId, monitored));
    }

    public void SetHasMissingEpisodes(bool hasMissingEpisodes)
    {
        if (HasMissingEpisodes == hasMissingEpisodes)
        {
            return;
        }

        HasMissingEpisodes = hasMissingEpisodes;
        _domainEvents.Add(new ProgramMissingEpisodesChangedEvent(RuvId, hasMissingEpisodes));
    }

    public void MatchTvdb(TvdbSeries series)
    {
        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        Series = series;
        _domainEvents.Add(new ProgramMatchedTvdbEvent(RuvId, Name));
    }

    public void MatchTmdb(TmdbMovie movie)
    {
        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        Movie = movie;
        _domainEvents.Add(new ProgramMatchedTmdbEvent(RuvId, Name));
    }

    public void RequestRefresh()
    {
        _domainEvents.Add(new ProgramRefreshRequestedEvent(RuvId, Name, Series is not null, HasMultipleEpisodes));
    }

    public void ScheduleLookup()
    {
        LookupCount++;
        NextLookup = LookupSchedule.ComputeNextLookup(LookupCount);
    }

    public bool TryAddEpisode(string id, Uri uri, string? title, string description, DateTime firstRun, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        RuvEpisode? existing = _episodes.FirstOrDefault(x => x.RuvId == id);
        if (existing is not null)
        {
            return false;
        }

        RuvEpisode episode = RuvEpisode.Create(this, id, uri, title, description, firstRun, duration);
        _episodes.Add(episode);

        if (Series is not null)
        {
            _domainEvents.Add(new EpisodeAddedToMatchedProgramEvent(RuvId, Name));
        }

        return true;
    }

    public void RemoveEpisode(RuvEpisode episode)
    {
        _episodes.Remove(episode);
    }

    public static bool IsGenericEpisodeTitle(string name) =>
        GenericEpisodeTitlePattern().IsMatch(name);

    [GeneratedRegex(@"^[Þþ]áttur \d+ af \d+$")]
    private static partial Regex GenericEpisodeTitlePattern();

    public int ResolveMatchingSeason(IEnumerable<Episode> tvdbEpisodes)
    {
        List<Episode> nonSpecials = [.. tvdbEpisodes.Where(x => x.SeasonNumber > 0)];

        if (SeasonNumber > 0)
        {
            return SeasonNumber;
        }

        int matchingSeasonCount = nonSpecials
            .GroupBy(x => x.SeasonNumber)
            .Count(g => g.Count() == _episodes.Count);

        return matchingSeasonCount == 1
            ? nonSpecials
                .GroupBy(x => x.SeasonNumber)
                .First(g => g.Count() == _episodes.Count)
                .Key
            : 0;
    }
}