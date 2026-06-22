using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup.Strategies;

internal sealed class EpisodeNumberMatchingStrategy(
    ILogger<EpisodeNumberMatchingStrategy> logger) : IEpisodeMatchingStrategy
{
    public Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken)
    {
        List<RuvEpisode> unmatchedEpisodes = [.. context.Program.Episodes.Where(x => x.TvdbEpisodes.Count == 0)];

        if (unmatchedEpisodes.Count > 0)
        {
            int season = context.Program.ResolveMatchingSeason(context.SeriesData.Episodes);

            if (season > 0)
            {
                List<Episode> tvdbSeasonEpisodes = [.. context.SeriesData.Episodes.Where(x => x.SeasonNumber == season)];

                if (tvdbSeasonEpisodes.Count == context.Program.Episodes.Count)
                {
                    foreach (RuvEpisode unmatchedEpisode in unmatchedEpisodes)
                    {
                        if (!unmatchedEpisode.TryGetEpisodeNumber(out int episodeNumber))
                        {
                            continue;
                        }

                        Episode? tvdbEpisode = tvdbSeasonEpisodes.FirstOrDefault(x => x.Number == episodeNumber);

                        if (tvdbEpisode is null)
                        {
                            continue;
                        }

                        unmatchedEpisode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, context.MissingTvdbIds.Contains(tvdbEpisode.Id), tvdbEpisode.NameTranslations?.Contains("isl") ?? false);
                        logger.LogInformation(
                            "Matched RÚV episode {Episode} with TVDB episode '{TvdbSeries}' S{TvdbSeason:D2}E{TvdbEpisode:D2} '{TvdbEpisodeName}' via season/episode fallback",
                            unmatchedEpisode.ToString(),
                            context.SeriesData.Series.Name,
                            tvdbEpisode.SeasonNumber,
                            tvdbEpisode.Number,
                            tvdbEpisode.Name);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
