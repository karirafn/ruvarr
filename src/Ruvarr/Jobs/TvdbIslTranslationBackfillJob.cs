using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbIslTranslationBackfillJob(
    ILogger<TvdbIslTranslationBackfillJob> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    ISettingsStore settingsStore) : IJob
{
    private const int DelayBetweenSeriesFetchesMs = 500;

    public async Task Execute(IJobExecutionContext context)
    {
        RuvarrSettings settings = settingsStore.Current;

        if (settings.IslTranslationBackfillComplete)
        {
            logger.LogDebug("ISL translation backfill already complete, skipping");
            return;
        }

        if (!settings.IsTvdbConfigured)
        {
            logger.LogDebug("TVDB not configured, skipping ISL translation backfill");
            return;
        }

        logger.LogInformation("Starting ISL translation backfill");

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(p => p.Series != null)
            .Where(p => p.Episodes.Any(e => e.TvdbEpisodes.Any()))
            .Include(p => p.Episodes)
                .ThenInclude(e => e.TvdbEpisodes)
            .ToListAsync(context.CancellationToken);

        bool isFirst = true;
        foreach (IGrouping<int, RuvProgram> group in programs.GroupBy(p => p.Series!.TvdbId))
        {
            if (!isFirst)
            {
                await Task.Delay(DelayBetweenSeriesFetchesMs, context.CancellationToken);
            }
            isFirst = false;

            int seriesTvdbId = group.Key;

            SeriesData? seriesData = await tvdb.GetSeriesAsync(seriesTvdbId, context.CancellationToken);
            if (seriesData is null)
            {
                logger.LogWarning("TVDB series {SeriesId} not found during backfill", seriesTvdbId);
                continue;
            }

            Dictionary<int, Episode> tvdbEpisodesById = seriesData.Episodes.ToDictionary(e => e.Id);

            foreach (RuvProgram program in group)
            {
                foreach (RuvEpisode ruvEpisode in program.Episodes)
                {
                    foreach (TvdbEpisode tvdbEpisode in ruvEpisode.TvdbEpisodes)
                    {
                        if (tvdbEpisodesById.TryGetValue(tvdbEpisode.TvdbId, out Episode? remoteTvdbEpisode))
                        {
                            tvdbEpisode.HasIslTranslation = remoteTvdbEpisode.NameTranslations?.Contains("isl") ?? false;
                        }
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(context.CancellationToken);

        await settingsStore.SaveAsync(
            settings with { IslTranslationBackfillComplete = true },
            context.CancellationToken);

        logger.LogInformation("ISL translation backfill complete");
    }
}
