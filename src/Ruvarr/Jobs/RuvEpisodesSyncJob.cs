using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv;
using Ruvarr.Ruv.Domain;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

internal sealed class RuvEpisodesSyncJob(ILogger<RuvEpisodesSyncJob> logger, IRuvClient ruv, RuvarrDbContext dbContext) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV episode sync job");

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .ToListAsync();
        logger.LogDebug("Found {Count} RÚV programs with multiple episodes in database", programs.Count);

        foreach (RuvProgram program in programs)
        {
            logger.LogDebug("Getting episodes for RÚV program '{Name}'", program.Name);

            RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(program.RuvId);

            if (ruvProgram is null)
            {
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

            _ = await dbContext.SaveChangesAsync();
        }
    }
}