using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup.Strategies;

internal sealed class TranslationMatchingStrategy(
    ITvdbClient tvdb,
    ILogger<TranslationMatchingStrategy> logger) : IEpisodeMatchingStrategy
{
    public async Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken)
    {
        List<RuvEpisode> unmatchedEpisodes = [.. context.Program.Episodes.Where(x => x.TvdbEpisodes.Count == 0)];

        if (unmatchedEpisodes.Count == 0)
        {
            return;
        }

        HashSet<int> linkedIds = [.. context.Program.Episodes.SelectMany(x => x.TvdbEpisodes).Select(x => x.TvdbId)];

        logger.LogDebug("Series {Name} has {Count} episodes", context.SeriesData.Series.Name, context.SeriesData.Episodes.Count);
        List<Episode> unlinkedEpisodes = [.. context.SeriesData.Episodes.Where(x => !linkedIds.Contains(x.Id))];
        IReadOnlyList<IslTitle> titles = await context.IslTitles.GetAsync(tvdb, unlinkedEpisodes, cancellationToken);
        logger.LogDebug("Found {Count} episodes with Icelandic titles", titles.Count);

        SeasonConstraint constraint = context.SeasonConstraint;

        List<(RuvEpisode Episode, Episode TvdbEpisode)> selections = [];
        foreach (RuvEpisode episode in unmatchedEpisodes)
        {
            Episode? selected = constraint.Select(IslTitleLookup.CandidatesFor(episode, titles));

            if (selected is not null)
            {
                selections.Add((episode, selected));
            }
        }

        // Two RÚV episodes picking the same TVDB episode cannot both be right, so neither is linked.
        foreach ((RuvEpisode episode, Episode tvdbEpisode) in selections
            .GroupBy(x => x.TvdbEpisode.Id)
            .Where(g => g.Count() == 1)
            .Select(g => g.Single()))
        {
            episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, context.SonarrStatuses, MatchOrigin.Automatic, hasIslTranslation: true);
            logger.LogInformation(
                "Matched RÚV episode {Episode} with TVDB episode '{TvdbSeries}' - '{TvdbEpisodeName}'",
                episode.ToString(),
                context.SeriesData.Series.Name,
                tvdbEpisode.Name);
        }
    }
}
