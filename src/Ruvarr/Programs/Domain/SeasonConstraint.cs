using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Programs.Domain;

internal enum MatchVerdict
{
    Kept,
    Rejected,
}

/// <summary>
/// The rule season-constrained translation matching uses to pick among a RÚV episode's title candidates:
/// strict (declared season only) for a year-named program in a year-numbered series, preferred otherwise.
/// </summary>
internal sealed record SeasonConstraint
{
    public const int YearSeasonThreshold = 1900;

    private SeasonConstraint(int declaredSeason, bool isStrict)
    {
        DeclaredSeason = declaredSeason;
        IsStrict = isStrict;
    }

    /// <summary>The program's declared season; 0 when it declares none.</summary>
    public int DeclaredSeason { get; }

    public bool IsStrict { get; }

    public static SeasonConstraint Resolve(int declaredSeason, IEnumerable<int> seriesSeasonNumbers)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(declaredSeason);
        ArgumentNullException.ThrowIfNull(seriesSeasonNumbers);

        bool isStrict = declaredSeason >= YearSeasonThreshold
            && seriesSeasonNumbers.Any(s => s >= YearSeasonThreshold);

        return new SeasonConstraint(declaredSeason, isStrict);
    }

    public Episode? Select(IReadOnlyCollection<Episode> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<Episode> inDeclaredSeason = [.. candidates.Where(IsInDeclaredSeason)];

        if (IsStrict || inDeclaredSeason.Count > 0)
        {
            return SingleOrNone(inDeclaredSeason);
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
            return currentLink.SeasonNumber == DeclaredSeason ? MatchVerdict.Kept : MatchVerdict.Rejected;
        }

        Episode? selected = Select(candidates);

        return selected is not null && selected.Id != currentLink.TvdbId
            ? MatchVerdict.Rejected
            : MatchVerdict.Kept;
    }

    private bool IsInDeclaredSeason(Episode candidate) =>
        DeclaredSeason > 0 && candidate.SeasonNumber == DeclaredSeason;

    private static Episode? SingleOrNone(IReadOnlyCollection<Episode> candidates) =>
        candidates.Count == 1 ? candidates.First() : null;
}
