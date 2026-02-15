
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Tvdb.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbEpisodeLookupJob(ILogger<TvdbEpisodeLookupJob> logger, RuvarrDbContext dbContext, ITvdbClient tvdb) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting TVDB episode lookup job");

        RuvProgram? program = await dbContext.Set<RuvEpisode>()
            .Where(x => x.Program.Series!.TvdbId != null)
            .Where(x => x.TvdbId == null)
            .Where(e => e.NextLookup == null || e.NextLookup <= DateTime.UtcNow)
            .OrderBy(e => e.NextLookup)
            .Select(e => e.Program)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null || program.Series is null || !int.TryParse(program.Series.TvdbId, out int seriesId) || seriesId < 1)
        {
            logger.LogDebug("No RÚV program pending TVDB episode lookup");
            return;
        }

        logger.LogDebug("Getting TVDB series data");
        SeriesData? seriesData = await tvdb.GetSeriesAsync(seriesId)
            .ConfigureAwait(false);

        if (seriesData is null)
        {
            await ScheduleLookupAsync(program).ConfigureAwait(false);

            return;
        }

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
            EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(translatedEpisode.Id)
                .ConfigureAwait(false);

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
            episode.Match(translatedEpisode.Id, translatedEpisode.SeasonNumber, translatedEpisode.Number);
        }

        await ScheduleLookupAsync(program).ConfigureAwait(false);
    }

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        foreach (RuvEpisode episode in program.Episodes.Where(x => x.TvdbId is null))
        {
            episode.ScheduleLookup();
        }

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}