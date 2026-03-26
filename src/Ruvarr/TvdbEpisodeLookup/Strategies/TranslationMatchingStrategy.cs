using System.Collections.Concurrent;

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
        HashSet<int> matchedIds = [.. context.Program.Episodes.SelectMany(x => x.TvdbEpisodes).Select(x => x.TvdbId)];

        logger.LogDebug("Series {Name} has {Count} episodes", context.SeriesData.Series.Name, context.SeriesData.Episodes.Count);
        List<Episode> translatedEpisodes = [.. context.SeriesData.Episodes
            .Where(x => !matchedIds.Contains(x.Id))
            .Where(x => x.NameTranslations.Contains("isl"))];
        logger.LogDebug("Found {Count} episodes with Icelandic titles", translatedEpisodes.Count);

        ConcurrentBag<(Episode Episode, EpisodeTranslation? Translation)> translations = [];

        await Parallel.ForEachAsync(
            translatedEpisodes,
            new ParallelOptions { MaxDegreeOfParallelism = 3, CancellationToken = cancellationToken },
            async (translatedEpisode, ct) =>
            {
                logger.LogDebug(
                    "Querying TVDB translation for {SeriesName} S{Season:D2}E{Episode:D2} {EpisodeName}",
                    context.SeriesData.Series.Name,
                    translatedEpisode.SeasonNumber,
                    translatedEpisode.Number,
                    translatedEpisode.Name);
                EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(translatedEpisode.Id, cancellationToken: ct);
                translations.Add((translatedEpisode, translation));
            });

        foreach ((Episode translatedEpisode, EpisodeTranslation? translation) in translations)
        {
            if (translation is null)
            {
                logger.LogDebug("TVDB episode translation not found");
                continue;
            }

            List<RuvEpisode> episodes = [.. context.Program.Episodes.Where(x => x.IsMatch(translation.Name))];

            if (episodes.Count != 1)
            {
                continue;
            }

            RuvEpisode episode = episodes[0];

            episode.Match(translatedEpisode.Id, translatedEpisode.SeasonNumber, translatedEpisode.Number, context.MissingTvdbIds.Contains(translatedEpisode.Id));
            logger.LogInformation(
                "Matched RÚV episode {Episode} with TVDB episode '{TvdbSeries}' - '{TvdbEpisodeName}'",
                episode.ToString(),
                context.SeriesData.Series.Name,
                translatedEpisode.Name);
        }
    }
}
