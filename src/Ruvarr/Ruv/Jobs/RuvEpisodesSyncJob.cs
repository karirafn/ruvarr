using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv.Domain;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Ruv.Jobs;

internal sealed class RuvEpisodesSyncJob(ILogger<RuvEpisodesSyncJob> logger, IRuvClient ruv, RuvarrDbContext dbContext) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Starting RÚV episode sync job");

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .ToListAsync()
            .ConfigureAwait(false);
        logger.LogInformation("Found {Count} programs with multiple episodes in database", programs.Count);

        foreach (RuvProgram program in programs)
        {
            logger.LogInformation("Getting episodes for RÚV program '{Name}'", program.Name);

            RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(program.RuvId)
                .ConfigureAwait(false);

            if (ruvProgram is null)
            {
                continue;
            }

            logger.LogInformation("Adding episodes to RÚV program '{Name}'", program.Name);
            foreach (RuvTvEpisode episode in ruvProgram.Episodes)
            {
                program.TryAddEpisode(episode.Id, episode.File, episode.Title);
            }

            await dbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }
    }
}