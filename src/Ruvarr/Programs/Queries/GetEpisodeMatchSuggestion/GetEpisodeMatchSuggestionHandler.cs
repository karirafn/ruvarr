using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup;

namespace Ruvarr.Programs.Queries.GetEpisodeMatchSuggestion;

/// <summary>
/// Works out what the manual match dialog pre-selects for an unmatched RÚV episode. Returns <c>null</c> when the
/// episode is unknown or its program's TVDB series cannot be loaded.
/// </summary>
internal sealed class GetEpisodeMatchSuggestionHandler(RuvarrDbContext dbContext, ITvdbClient tvdb)
    : IRequestHandler<GetEpisodeMatchSuggestionQuery, EpisodeMatchSuggestion?>
{
    public async Task<EpisodeMatchSuggestion?> Handle(GetEpisodeMatchSuggestionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .AsNoTracking()
            .Include(x => x.Episodes).ThenInclude(x => x.TvdbEpisodes)
            .Where(x => x.Episodes.Any(e => e.RuvId == request.RuvId))
            .FirstOrDefaultAsync(cancellationToken);

        if (program?.Series is null)
        {
            return null;
        }

        SeriesData? seriesData = await tvdb.GetSeriesAsync(program.Series.TvdbId, cancellationToken);

        if (seriesData is null)
        {
            return null;
        }

        RuvEpisode episode = program.Episodes.Single(x => x.RuvId == request.RuvId);
        List<Episode> regularEpisodes = [.. seriesData.Episodes.Where(x => x.SeasonNumber > 0)];
        List<int> seasons = [.. regularEpisodes.Select(x => x.SeasonNumber).Distinct().Order()];
        SeasonConstraint constraint = program.ResolveSeasonConstraint(seasons);
        IReadOnlyList<int> declaredSeasons = [.. constraint.DeclaredSeasons.Order()];
        int? defaultSeason = ResolveDefaultSeason(program, declaredSeasons, seasons);

        SuggestedEpisode? suggestion = episode.TvdbEpisodes.Count > 0
            ? null
            : await SuggestOutsideDeclaredSeasonAsync(program, episode, constraint, regularEpisodes, cancellationToken)
                ?? SuggestByEpisodeNumber(episode, defaultSeason, regularEpisodes);

        return new EpisodeMatchSuggestion(suggestion, defaultSeason, declaredSeasons);
    }

    private async Task<SuggestedEpisode?> SuggestOutsideDeclaredSeasonAsync(
        RuvProgram program,
        RuvEpisode episode,
        SeasonConstraint constraint,
        IReadOnlyList<Episode> regularEpisodes,
        CancellationToken cancellationToken)
    {
        // Without a declared season nothing is outside it, so the translation calls would be wasted.
        if (constraint.DeclaredSeasons.Count == 0)
        {
            return null;
        }

        // Mirrors translation matching: an episode already linked elsewhere is never a candidate.
        HashSet<int> linkedIds = [.. program.Episodes.SelectMany(x => x.TvdbEpisodes).Select(x => x.TvdbId)];
        List<Episode> unlinked = [.. regularEpisodes.Where(x => !linkedIds.Contains(x.Id))];
        IReadOnlyList<IslTitle> titles = await new IslTitleLookup().GetAsync(tvdb, unlinked, cancellationToken);

        Episode? suggested = constraint.SuggestOutsideDeclaredSeason(IslTitleLookup.CandidatesFor(episode, titles));

        return suggested is null ? null : new SuggestedEpisode(suggested.Id, MatchSuggestionReason.OutsideDeclaredSeason);
    }

    private static SuggestedEpisode? SuggestByEpisodeNumber(RuvEpisode episode, int? defaultSeason, IReadOnlyList<Episode> regularEpisodes)
    {
        if (defaultSeason is null || !episode.TryGetEpisodeNumber(out int number))
        {
            return null;
        }

        Episode? suggested = regularEpisodes.FirstOrDefault(x => x.SeasonNumber == defaultSeason && x.Number == number);

        return suggested is null ? null : new SuggestedEpisode(suggested.Id, MatchSuggestionReason.EpisodeNumber);
    }

    private static int? ResolveDefaultSeason(RuvProgram program, IReadOnlyList<int> declaredSeasons, List<int> seasons)
    {
        IEnumerable<int> lowestLinkedSeason = program.Episodes
            .SelectMany(x => x.TvdbEpisodes)
            .Select(x => x.SeasonNumber)
            .Where(x => x > 0)
            .Order()
            .Take(1);

        return declaredSeasons.Take(1)
            .Concat(lowestLinkedSeason)
            .Where(seasons.Contains)
            .Cast<int?>()
            .FirstOrDefault()
            ?? (seasons.Count > 0 ? seasons[0] : null);
    }
}
