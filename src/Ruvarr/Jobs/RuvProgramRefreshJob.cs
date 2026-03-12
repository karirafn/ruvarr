using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.Programs;
using Ruvarr.Programs.Domain;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

internal sealed class RuvProgramRefreshJob(
    ILogger<RuvProgramRefreshJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    IOptions<RuvarrOptions> options,
    ProgramRefreshNotifier syncQueue) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV programs refresh job");

        RuvFeaturedTv? kids = await ruv.GetKidsTvAsync();
        List<RuvTvProgram> kidsPograms = kids?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} Krakka RÚV programs", kidsPograms.Count);

        RuvFeaturedTv? featured = await ruv.GetFeaturedTv();
        List<RuvTvProgram> featuredPrograms = featured?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} featured RÚV programs", featuredPrograms.Count);

        List<RuvTvProgram> allPrograms = [.. featuredPrograms, .. kidsPograms];
        logger.LogDebug("Found {Count} total RÚV programs", allPrograms.Count);

        List<RuvTvProgram> programs = [.. allPrograms
            .Where(x => !options.Value.IgnoredChannels.Contains(x.Channel))
            .Where(x => x.WebAvailableEpisodes > 0)
            .DistinctBy(x => x.Id)];
        logger.LogDebug("Found {Count} distinct RÚV programs", programs.Count);

        List<int> ruvIds = [.. programs.Select(x => x.Id)];

        if (ruvIds is [])
        {
            return;
        }

        List<RuvProgram> existingTvPrograms = await dbContext.Set<RuvProgram>()
            .Where(x => ruvIds.Contains(x.RuvId))
            .ToListAsync();
        logger.LogDebug("Found {Count} RÚV programs in database", existingTvPrograms.Count);

        List<int> existingRuvIds = [.. existingTvPrograms.Select(x => x.RuvId)];
        List<RuvProgram> removedPrograms = [.. existingTvPrograms.Where(x => !ruvIds.Contains(x.RuvId))];

        if (removedPrograms.Count > 0)
        {
            logger.LogInformation("Removing {Count} RÚV programs from database", removedPrograms.Count);
        }

        List<RuvProgram> newPrograms = [.. programs
            .Where(x => !existingRuvIds.Contains(x.Id))
            .Select(x => RuvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes))];

        if (newPrograms.Count > 0)
        {
            logger.LogInformation("Adding {Count} RÚV new programs to database", newPrograms.Count);
        }

        dbContext.Set<RuvProgram>()
            .RemoveRange(removedPrograms);

        dbContext.Set<RuvProgram>()
            .AddRange(newPrograms);

        await dbContext.SaveChangesAsync();

        foreach (RuvTvProgram program in programs.Where(x => x.MultipleEpisodes))
        {
            syncQueue.Enqueue(program.Id, program.Title);
        }
    }
}