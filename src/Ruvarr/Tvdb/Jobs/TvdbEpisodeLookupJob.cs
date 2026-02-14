
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
        logger.LogInformation("Starting TVDB episode lookup job");

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(p => p.Series!.TvdbId != null)
            .SelectMany(p => p.Episodes)
            .Where(e => e.TvdbId == null)
            .Where(e => e.NextLookup == null || e.NextLookup <= DateTime.UtcNow)
            .OrderBy(e => e.NextLookup)
            .Select(e => e.Program)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null || program.Series is null || !int.TryParse(program.Series.TvdbId, out int seriesId) || seriesId < 1)
        {
            logger.LogInformation("No RÚV program pending TVDB episode lookup");
            return;
        }

        logger.LogInformation("Getting TVDB series data");
        SeriesData? seriesData = await tvdb.GetSeriesAsync(seriesId)
            .ConfigureAwait(false);

        if (seriesData is null)
        {
            await ScheduleLookupAsync(program).ConfigureAwait(false);

            return;
        }

        logger.LogInformation("Series {Name} has {Count} episodes", seriesData.Series.Name, seriesData.Episodes.Count);
        List<Episode> translatedEpisodes = [.. seriesData.Episodes.Where(x => x.NameTranslations.Contains("isl"))];
        logger.LogInformation("Found {Count} episodes with Icelandic titles", translatedEpisodes.Count);

        foreach (Episode translatedEpisode in translatedEpisodes)
        {
            logger.LogInformation(
                "Querying TVDB translation for {SeriesName} S{Season:D2}E{Episode:D2} {EpisodeName}",
                seriesData.Series.Name,
                translatedEpisode.SeasonNumber,
                translatedEpisode.Number,
                translatedEpisode.Name);
            EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(translatedEpisode.Id)
                .ConfigureAwait(false);

            if (translation is null)
            {
                logger.LogWarning("TVDB episode translation not found");
                continue;
            }

            RuvEpisode? episode = program.Episodes
                .SingleOrDefault(x => x.Title == translation.Name);

            if (episode is null)
            {
                logger.LogInformation("TVDB episode translation did not match any RÚV episodes");
                continue;
            }

            logger.LogInformation(
                "TVDB episode {SeriesName} S{Season:D2}E{Episode:D2} {EpisodeName} matched RÚV episode {Title}",
                seriesData.Series.Name,
                translatedEpisode.SeasonNumber,
                translatedEpisode.Number,
                translatedEpisode.Name,
                episode.Title);
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