using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup.Strategies;

internal sealed class PartOneSiblingMatchingStrategy(
    ILogger<PartOneSiblingMatchingStrategy> logger) : IEpisodeMatchingStrategy
{
    public Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken)
    {
        List<RuvEpisode> unmatchedPartTwoEpisodes = [.. context.Program.Episodes
            .Where(x => x.TvdbEpisodes.Count == 0)
            .Where(x => RuvEpisode.IsPartTwo(x.Title))];

        foreach (RuvEpisode partTwoEpisode in unmatchedPartTwoEpisodes)
        {
            string partOneTitle = RuvEpisode.ToPartOneTitle(partTwoEpisode.Title);

            RuvEpisode? partOneSibling = context.Program.Episodes
                .FirstOrDefault(x => x.TvdbEpisodes.Count > 0
                    && x.Title.Equals(partOneTitle, StringComparison.OrdinalIgnoreCase));

            if (partOneSibling is null)
            {
                continue;
            }

            TvdbEpisode? firstMatch = partOneSibling.TvdbEpisodes
                .OrderBy(e => e.SeasonNumber)
                .ThenBy(e => e.EpisodeNumber)
                .FirstOrDefault();

            if (firstMatch is null)
            {
                continue;
            }

            Episode? tvdbPartOneEpisode = context.SeriesData.Episodes
                .FirstOrDefault(x => x.Id == firstMatch.TvdbId);

            if (tvdbPartOneEpisode is null)
            {
                continue;
            }

            string? partTwoName = ToPartTwoName(tvdbPartOneEpisode.Name);

            if (partTwoName is null)
            {
                continue;
            }

            Episode? tvdbPartTwoEpisode = context.SeriesData.Episodes
                .FirstOrDefault(x => x.Name.Equals(partTwoName, StringComparison.OrdinalIgnoreCase));

            if (tvdbPartTwoEpisode is null)
            {
                continue;
            }

            partTwoEpisode.Match(tvdbPartTwoEpisode.Id, tvdbPartTwoEpisode.SeasonNumber, tvdbPartTwoEpisode.Number, context.MissingTvdbIds.Contains(tvdbPartTwoEpisode.Id), tvdbPartTwoEpisode.NameTranslations?.Contains("isl") ?? false);
            logger.LogInformation(
                "Matched RÚV episode {Episode} with TVDB episode '{TvdbSeries}' S{TvdbSeason:D2}E{TvdbEpisode:D2} '{TvdbEpisodeName}' via part-one sibling fallback",
                partTwoEpisode.ToString(),
                context.SeriesData.Series.Name,
                tvdbPartTwoEpisode.SeasonNumber,
                tvdbPartTwoEpisode.Number,
                tvdbPartTwoEpisode.Name);
        }

        return Task.CompletedTask;
    }

    private static string? ToPartTwoName(string tvdbEpisodeName)
    {
        if (tvdbEpisodeName.EndsWith(" Part 1", StringComparison.OrdinalIgnoreCase))
        {
            return string.Concat(tvdbEpisodeName.AsSpan(0, tvdbEpisodeName.Length - 1), "2");
        }

        if (tvdbEpisodeName.EndsWith("(1)", StringComparison.Ordinal))
        {
            return string.Concat(tvdbEpisodeName.AsSpan(0, tvdbEpisodeName.Length - 2), "2)");
        }

        return null;
    }
}
