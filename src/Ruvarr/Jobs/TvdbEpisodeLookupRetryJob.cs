using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbEpisodeLookupRetryJob(
    ILogger<TvdbEpisodeLookupRetryJob> logger,
    RuvarrDbContext dbContext,
    TvdbEpisodeLookupNotifier notifier) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting TVDB episode lookup retry job");

        DateTime utcNow = DateTime.UtcNow;

        List<RuvProgram> programs = await dbContext.Set<RuvProgram>()
            .Where(x => x.Series != null)
            .Where(x => x.Episodes.Any(e => e.TvdbId == null && e.NextLookup != null && e.NextLookup <= utcNow))
            .ToListAsync();

        foreach (RuvProgram program in programs)
        {
            logger.LogInformation("Enqueuing TVDB episode lookup retry for program '{Name}'", program.Name);
            notifier.Enqueue(program.RuvId, program.Name);
        }
    }
}
