using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Ruv.Domain;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Ruv.Jobs;

internal sealed class RuvEpisodesSyncJob(IRuvClient ruv, RuvarrDbContext dbContext) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (RuvProgram program in programs)
        {
            RuvTvProgram? ruvProgram = await ruv.GetProgramAsync(program.RuvId)
                .ConfigureAwait(false);

            if (ruvProgram is null)
            {
                continue;
            }

            foreach (RuvTvEpisode episode in ruvProgram.Episodes)
            {
                program.TryAddEpisode(episode.Id, episode.File, episode.Title);
            }

            await dbContext.SaveChangesAsync()
                .ConfigureAwait(false);
        }
    }
}