using System.Globalization;
using System.Text.RegularExpressions;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Events;
using Ruvarr.RomanNumerals;

namespace Ruvarr.Programs.Domain;

internal sealed partial class RuvProgram
{
    private const int MaxDescriptionLength = 4096;

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

    /// <summary>Set by a manual refresh; the next TVDB episode lookup re-checks automatic links, then clears it.</summary>
    public bool MatchRecheckRequested { get; private set; }

    public string? Slug { get; private set; }

    public string? Description { get; private set; }

    public Uri? ImageUrl { get; private set; }

    public TvdbSeries? Series { get; private set; }

    public TmdbMovie? Movie { get; private set; }

    public IReadOnlyList<RuvEpisode> Episodes => _episodes.AsReadOnly();

    /// <summary>Set by the user; takes precedence over the season parsed from the name. Null when Automatic.</summary>
    public ManualSeasons? ManualSeasons { get; private set; }

    public ParsedSeason? ParsedSeason => ParseSeason(Name, ForeignName);

    public int SeasonNumber => ParsedSeason?.Number ?? 0;

    /// <summary>The manual seasons when set, otherwise the season parsed from the name; ascending, possibly empty.</summary>
    public IReadOnlyList<int> DeclaredSeasons =>
        ManualSeasons?.Numbers ?? (SeasonNumber > 0 ? [SeasonNumber] : []);

    /// <summary>
    /// How many episodes RÚV says a season has: M from "Þáttur N af M" when every generic title gives the same M,
    /// otherwise the number of published episodes.
    /// </summary>
    public int ExpectedSeasonLength
    {
        get
        {
            List<int> lengths = [.. _episodes
                .Select(x => GenericEpisodeTitlePattern().Match(x.Title))
                .Where(x => x.Success)
                .Select(x => int.Parse(x.Groups["of"].ValueSpan, CultureInfo.InvariantCulture))
                .Distinct()];

            return lengths.Count == 1 ? lengths[0] : _episodes.Count;
        }
    }

    public static ParsedSeason? ParseSeason(string name, string? foreignName) =>
        ParseSeasonFromSuffix(name) ?? ParseSeasonFromSuffix(foreignName);

    private static ParsedSeason? ParseSeasonFromSuffix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string suffix = name.Split(' ')[^1];

        if (RomanNumeral.TryParse(suffix, out RomanNumeral? rn) && rn.Number > 0)
        {
            return new ParsedSeason(suffix, rn.Number);
        }

        if (int.TryParse(suffix, out int n) && n > 0)
        {
            return new ParsedSeason(suffix, n);
        }

        return null;
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
        slug is not null && SlugPattern().IsMatch(slug) ? slug : null;

    [GeneratedRegex(@"^[a-z0-9\-]{1,128}$")]
    private static partial Regex SlugPattern();

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
        ArgumentNullException.ThrowIfNull(series);

        // A season number only means something for its own series.
        if (Series is not null && Series.TvdbId != series.TvdbId)
        {
            ManualSeasons = null;
        }

        LookupCount++;
        NextLookup = null;
        Matched = DateTime.UtcNow;
        Series = series;
        _domainEvents.Add(new ProgramMatchedTvdbEvent(RuvId, Name));
    }

    public RuvarrResult SetManualSeasons(IEnumerable<int> seasons, IEnumerable<int> seriesSeasonNumbers)
    {
        ArgumentNullException.ThrowIfNull(seasons);
        ArgumentNullException.ThrowIfNull(seriesSeasonNumbers);

        if (Series is null)
        {
            return ProgramErrors.ManualSeasonsRequireSeries;
        }

        Result<ManualSeasons> created = ManualSeasons.Create(seasons);
        if (created.IsFailure)
        {
            return created.Error;
        }

        ManualSeasons manualSeasons = created.FromResult();
        HashSet<int> regularSeasons = [.. seriesSeasonNumbers.Where(x => x > 0)];
        if (!manualSeasons.Numbers.All(regularSeasons.Contains))
        {
            return ProgramErrors.ManualSeasonNotInSeries;
        }

        ManualSeasons = manualSeasons;
        return RuvarrResult.Success;
    }

    public void ClearManualSeasons() => ManualSeasons = null;

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
        // Mirrors when ProgramRefreshRequestedEventHandler enqueues the TVDB episode lookup, the only clearer of the flag.
        if (Series is not null && HasMultipleEpisodes)
        {
            MatchRecheckRequested = true;
        }

        _domainEvents.Add(new ProgramRefreshRequestedEvent(RuvId, Name, Series is not null, HasMultipleEpisodes));
    }

    public void CompleteMatchRecheck() => MatchRecheckRequested = false;

    public void ScheduleLookup()
    {
        LookupCount++;
        NextLookup = LookupSchedule.ComputeNextLookup(LookupCount);
    }

    /// <summary>
    /// Adds a new episode or, when an episode with the same RuvId already exists, updates its title
    /// if it differs. Returns true only when a new episode is added.
    /// </summary>
    public bool TryAddEpisode(string id, Uri uri, string? title, string description, DateTime firstRun, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        RuvEpisode? existing = _episodes.FirstOrDefault(x => x.RuvId == id);
        if (existing is not null)
        {
            if (!existing.Title.Equals(title, StringComparison.Ordinal))
            {
                existing.UpdateTitle(title);
            }

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

    [GeneratedRegex(@"^[Þþ]áttur \d+ af (?<of>\d+)$")]
    private static partial Regex GenericEpisodeTitlePattern();

    /// <summary>
    /// The TVDB season Episode Number matching aligns the program with: the declared season when there is exactly
    /// one, none when there are several, otherwise the unique season whose episode count equals the
    /// expected season length. 0 when unresolved.
    /// </summary>
    public int ResolveMatchingSeason(IEnumerable<Episode> tvdbEpisodes)
    {
        ArgumentNullException.ThrowIfNull(tvdbEpisodes);

        IReadOnlyList<int> declaredSeasons = DeclaredSeasons;

        if (declaredSeasons.Count > 0)
        {
            // "Þáttur N" numbering across several seasons is unknown, so only a single declared season aligns.
            return declaredSeasons.Count == 1 ? declaredSeasons[0] : 0;
        }

        int expectedSeasonLength = ExpectedSeasonLength;
        List<int> matchingSeasons = [.. tvdbEpisodes
            .Where(x => x.SeasonNumber > 0)
            .GroupBy(x => x.SeasonNumber)
            .Where(g => g.Count() == expectedSeasonLength)
            .Select(g => g.Key)];

        return matchingSeasons.Count == 1 ? matchingSeasons[0] : 0;
    }

    public SeasonConstraint ResolveSeasonConstraint(IEnumerable<int> seriesSeasonNumbers) =>
        ManualSeasons is not null
            ? SeasonConstraint.StrictTo(ManualSeasons)
            : SeasonConstraint.Resolve(SeasonNumber, seriesSeasonNumbers);
}
