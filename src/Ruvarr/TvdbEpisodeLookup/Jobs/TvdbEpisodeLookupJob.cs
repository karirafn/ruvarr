using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.TvdbEpisodeLookup.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbEpisodeLookupJob(
    ILogger<TvdbEpisodeLookupJob> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    ISonarrClient sonarr,
    TvdbEpisodeLookupNotifier lookupQueue,
    IDomainEventBroadcaster broadcaster) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting TVDB episode lookup job");

        if (!lookupQueue.TryDequeue(out int ruvId))
        {
            logger.LogDebug("No RÚV program pending TVDB episode lookup");
            return;
        }

        lookupQueue.MarkProcessing(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.RuvId == ruvId)
            .FirstOrDefaultAsync();

        if (program is null || program.Series is null)
        {
            logger.LogDebug("No RÚV program pending TVDB episode lookup");
            lookupQueue.MarkComplete(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
            return;
        }

        logger.LogDebug("Getting TVDB series data");
        SeriesData? seriesData = await tvdb.GetSeriesAsync(program.Series.TvdbId);

        if (seriesData is null)
        {
            await ScheduleLookupAsync(program);
            lookupQueue.MarkComplete(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
            return;
        }

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync();
        HashSet<int> missingTvdbIds = [.. missingEpisodes.Select(x => x.TvdbId)];

        await MatchByTranslationAsync(program, seriesData, missingTvdbIds);
        MatchByEpisodeNumber(program, seriesData, missingTvdbIds);
        MatchByPartOneSibling(program, seriesData, missingTvdbIds);

        await ScheduleLookupAsync(program);
        lookupQueue.MarkComplete(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>());
    }

    private async Task MatchByTranslationAsync(RuvProgram program, SeriesData seriesData, HashSet<int> missingTvdbIds)
    {
        List<int> matchedIds = [.. program.Episodes.Select(x => x.TvdbId).OfType<int>()];

        logger.LogDebug("Series {Name} has {Count} episodes", seriesData.Series.Name, seriesData.Episodes.Count);
        List<Episode> translatedEpisodes = [.. seriesData.Episodes
            .Where(x => !matchedIds.Contains(x.Id))
            .Where(x => x.NameTranslations.Contains("isl"))];
        logger.LogDebug("Found {Count} episodes with Icelandic titles", translatedEpisodes.Count);

        foreach (Episode translatedEpisode in translatedEpisodes)
        {
            logger.LogDebug(
                "Querying TVDB translation for {SeriesName} S{Season:D2}E{Episode:D2} {EpisodeName}",
                seriesData.Series.Name,
                translatedEpisode.SeasonNumber,
                translatedEpisode.Number,
                translatedEpisode.Name);
            EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(translatedEpisode.Id);

            if (translation is null)
            {
                logger.LogDebug("TVDB episode translation not found");
                continue;
            }

            List<RuvEpisode> episodes = [.. program.Episodes.Where(x => x.IsMatch(translation.Name))];

            if (episodes.Count != 1)
            {
                continue;
            }

            RuvEpisode episode = episodes[0];

            logger.LogInformation(
                "Matched RÚV episode '{RuvEpisode}' of program '{ProgramName}' with TVDB episode '{SeriesName}' S{Season:D2}E{Episode:D2} '{EpisodeName}'",
                episode.Title,
                program.Name,
                seriesData.Series.Name,
                translatedEpisode.SeasonNumber,
                translatedEpisode.Number,
                translatedEpisode.Name);
            episode.Match(translatedEpisode.Id, translatedEpisode.SeasonNumber, translatedEpisode.Number, missingTvdbIds.Contains(translatedEpisode.Id));
        }
    }

    private void MatchByEpisodeNumber(RuvProgram program, SeriesData seriesData, HashSet<int> missingTvdbIds)
    {
        List<RuvEpisode> unmatchedEpisodes = [.. program.Episodes.Where(x => x.TvdbId is null)];

        if (unmatchedEpisodes.Count > 0)
        {
            int season = program.ResolveMatchingSeason(seriesData.Episodes);

            if (season > 0)
            {
                List<Episode> tvdbSeasonEpisodes = [.. seriesData.Episodes.Where(x => x.SeasonNumber == season)];

                if (tvdbSeasonEpisodes.Count == unmatchedEpisodes.Count)
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

                        logger.LogInformation(
                            "Matched RÚV episode '{RuvEpisode}' of program '{ProgramName}' with TVDB episode '{SeriesName}' S{Season:D2}E{Episode:D2} '{EpisodeName}' via season/episode fallback",
                            unmatchedEpisode.Title,
                            program.Name,
                            seriesData.Series.Name,
                            tvdbEpisode.SeasonNumber,
                            tvdbEpisode.Number,
                            tvdbEpisode.Name);
                        unmatchedEpisode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, missingTvdbIds.Contains(tvdbEpisode.Id));
                    }
                }
            }
        }
    }

    private void MatchByPartOneSibling(RuvProgram program, SeriesData seriesData, HashSet<int> missingTvdbIds)
    {
        List<RuvEpisode> unmatchedPartTwoEpisodes = [.. program.Episodes
            .Where(x => x.TvdbId is null)
            .Where(x => RuvEpisode.IsPartTwo(x.Title))];

        foreach (RuvEpisode partTwoEpisode in unmatchedPartTwoEpisodes)
        {
            string partOneTitle = RuvEpisode.ToPartOneTitle(partTwoEpisode.Title);

            RuvEpisode? partOneSibling = program.Episodes
                .FirstOrDefault(x => x.TvdbId is not null
                    && x.Title.Equals(partOneTitle, StringComparison.OrdinalIgnoreCase));

            if (partOneSibling is null)
            {
                continue;
            }

            Episode? tvdbPartOneEpisode = seriesData.Episodes
                .FirstOrDefault(x => x.Id == partOneSibling.TvdbId);

            if (tvdbPartOneEpisode is null)
            {
                continue;
            }

            string? partTwoName = ToPartTwoName(tvdbPartOneEpisode.Name);

            if (partTwoName is null)
            {
                continue;
            }

            Episode? tvdbPartTwoEpisode = seriesData.Episodes
                .FirstOrDefault(x => x.Name.Equals(partTwoName, StringComparison.OrdinalIgnoreCase));

            if (tvdbPartTwoEpisode is null)
            {
                continue;
            }

            logger.LogInformation(
                "Matched RÚV episode '{RuvEpisode}' of program '{ProgramName}' with TVDB episode '{SeriesName}' S{Season:D2}E{Episode:D2} '{EpisodeName}' via part-one sibling fallback",
                partTwoEpisode.Title,
                program.Name,
                seriesData.Series.Name,
                tvdbPartTwoEpisode.SeasonNumber,
                tvdbPartTwoEpisode.Number,
                tvdbPartTwoEpisode.Name);
            partTwoEpisode.Match(tvdbPartTwoEpisode.Id, tvdbPartTwoEpisode.SeasonNumber, tvdbPartTwoEpisode.Number, missingTvdbIds.Contains(tvdbPartTwoEpisode.Id));
        }
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

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbId is null))
        {
            episode.ScheduleLookup();
        }

        await dbContext.SaveChangesAsync();
    }
}
