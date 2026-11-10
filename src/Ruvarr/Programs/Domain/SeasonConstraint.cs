using System.Collections.Immutable;

using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Programs.Domain;

internal enum MatchVerdict
{
    Kept,
    Rejected,
}

/// <summary>
/// The rule season-constrained translation matching uses to pick among a RÚV episode's title candidates:
/// strict (declared seasons only) for manual seasons or a year-named program in a year-numbered series,
/// preferred otherwise.
/// </summary>
internal sealed record SeasonConstraint
{
    public const int YearSeasonThreshold = 1900;

    private readonly ImmutableSortedSet<int> _declaredSeasons;

    private SeasonConstraint(ImmutableSortedSet<int> declaredSeasons, bool isStrict)
    {
        _declaredSeasons = declaredSeasons;
        IsStrict = isStrict;
    }

    /// <summary>The program's declared seasons; empty when it declares none.</summary>
    public IReadOnlySet<int> DeclaredSeasons => _declaredSeasons;

    public bool IsStrict { get; }

    public static SeasonConstraint Resolve(int declaredSeason, IEnumerable<int> seriesSeasonNumbers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(declaredSeason);
        ArgumentNullException.ThrowIfNull(seriesSeasonNumbers);

        bool isStrict = declaredSeason >= YearSeasonThreshold
            && seriesSeasonNumbers.Any(s => s >= YearSeasonThreshold);

        ImmutableSortedSet<int> declaredSeasons = declaredSeason > 0 ? [declaredSeason] : [];

        return new SeasonConstraint(declaredSeasons, isStrict);
    }

    /// <summary>A user's assertion about which seasons the program belongs to is never second-guessed.</summary>
    public static SeasonConstraint StrictTo(ManualSeasons manualSeasons)
    {
        ArgumentNullException.ThrowIfNull(manualSeasons);

        return new SeasonConstraint([.. manualSeasons.Numbers], isStrict: true);
    }

    public Episode? Select(IReadOnlyCollection<Episode> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<Episode> inDeclaredSeasons = [.. candidates.Where(x => _declaredSeasons.Contains(x.SeasonNumber))];

        if (IsStrict || inDeclaredSeasons.Count > 0)
        {
            return SingleOrNone(inDeclaredSeasons);
        }

        // Falling back across seasons is only safe when the title points at one season; spread over
        // several, a recurring title cannot tell which one this program is.
        return candidates.Select(x => x.SeasonNumber).Distinct().Count() == 1
            ? SingleOrNone(candidates)
            : null;
    }

    public MatchVerdict Judge(TvdbEpisode currentLink, IReadOnlyCollection<Episode> candidates)
    {
        ArgumentNullException.ThrowIfNull(currentLink);
        ArgumentNullException.ThrowIfNull(candidates);

        if (IsStrict)
        {
            return _declaredSeasons.Contains(currentLink.SeasonNumber) ? MatchVerdict.Kept : MatchVerdict.Rejected;
        }

        Episode? selected = Select(candidates);

        return selected is not null && selected.Id != currentLink.TvdbId
            ? MatchVerdict.Rejected
            : MatchVerdict.Kept;
    }

    public bool Equals(SeasonConstraint? other) =>
        other is not null && IsStrict == other.IsStrict && _declaredSeasons.SequenceEqual(other._declaredSeasons);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(IsStrict);
        foreach (int season in _declaredSeasons)
        {
            hash.Add(season);
        }

        return hash.ToHashCode();
    }

    private static Episode? SingleOrNone(IReadOnlyCollection<Episode> candidates) =>
        candidates.Count == 1 ? candidates.First() : null;
}
