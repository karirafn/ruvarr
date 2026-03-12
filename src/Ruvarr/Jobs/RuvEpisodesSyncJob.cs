using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Programs;
using Ruvarr.Programs.Domain;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class RuvEpisodesSyncJob(
    ILogger<RuvEpisodesSyncJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    ProgramRefreshNotifier syncQueue) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV episode sync job");

        List<int> ruvIds = [.. syncQueue.DequeueAll()];

        if (ruvIds is [])
        {
            logger.LogDebug("No programs in refresh queue");
            return;
        }

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => ruvIds.Contains(x.RuvId))
            .Where(x => x.HasMultipleEpisodes)
            .ToListAsync();
#pragma warning disable CA1309 // Culture-sensitive comparison is intentional for Icelandic alphabetical ordering
        programs.Sort((a, b) => string.Compare(a.Name, b.Name, new CultureInfo("is-IS"), CompareOptions.None));
#pragma warning restore CA1309
        logger.LogDebug("Found {Count} RÚV programs with multiple episodes in refresh queue", programs.Count);

        HashSet<int> loadedIds = [.. programs.Select(p => p.RuvId)];

        foreach (RuvProgram program in programs)
        {
            syncQueue.MarkProcessing(program.RuvId);

            logger.LogDebug("Getting episodes for RÚV program '{Name}'", program.Name);

            RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(program.RuvId);

            if (ruvProgram is null)
            {
                logger.LogInformation("Deleting RÚV program {Name} and {Count} episodes", program.Name, program.Episodes.Count);
                dbContext.Set<RuvProgram>().Remove(program);
                await dbContext.SaveChangesAsync();
                syncQueue.MarkComplete(program.RuvId);
                continue;
            }

            logger.LogDebug("Adding episodes to RÚV program '{Name}'", program.Name);

            ruvProgram.Episodes
                .Where(e => program.TryAddEpisode(
                    id: e.Id,
                    uri: e.File,
                    title: e.Title,
                    description: e.Description.Count > 0 ? e.Description[0] : string.Empty,
                    firstRun: e.FirstRun))
                .ToList()
                .ForEach(e => logger.LogInformation("Added RÚV episode '{EpisodeName}' to program '{Name}'", e.Title, program.Name));

            logger.LogDebug("Removing episodes from RÚV program '{Name}'", program.Name);
            IEnumerable<RuvEpisode> removed = program.Episodes
                .Where(entity => !ruvProgram.Episodes.Select(episodeDto => episodeDto.Id).Contains(entity.RuvId));

            foreach (RuvEpisode episode in removed)
            {
                logger.LogInformation("Removed RÚV episode '{EpisodeName}' from program '{Name}'", episode.Title, program.Name);
                program.RemoveEpisode(episode);
            }

            await dbContext.SaveChangesAsync();
            syncQueue.MarkComplete(program.RuvId);
        }

        foreach (int ruvId in ruvIds.Where(id => !loadedIds.Contains(id)))
        {
            syncQueue.MarkComplete(ruvId);
        }
    }
}
