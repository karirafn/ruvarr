using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv;
using Ruvarr.Ruv.Domain;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

internal sealed class RuvProgramSyncJob(ILogger<RuvProgramSyncJob> logger, IRuvClient ruv, RuvarrDbContext dbContext) : IJob
{
    private static readonly string[] ExcludedChannels = ["Fréttastofa sjónvarps", "Íþróttadeild", "Rás 1", "Rás 2"];

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV programs sync job");

        RuvFeaturedTv? kids = await ruv.GetKidsTvAsync()
            .ConfigureAwait(false);
        List<RuvTvProgram> kidsPograms = kids?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} Krakka RÚV programs", kidsPograms.Count);

        RuvFeaturedTv? featured = await ruv.GetFeaturedTv()
            .ConfigureAwait(false);
        List<RuvTvProgram> featuredPrograms = featured?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} featured RÚV programs", featuredPrograms.Count);

        List<RuvTvProgram> allPrograms = [.. featuredPrograms, .. kidsPograms];
        logger.LogDebug("Found {Count} total RÚV programs", allPrograms.Count);

        List<RuvTvProgram> programs = [.. allPrograms
            .Where(x => !ExcludedChannels.Contains(x.Channel))
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
            .ToListAsync()
            .ConfigureAwait(false);
        logger.LogDebug("Found {Count} RÚV programs in database", existingTvPrograms.Count);

        List<int> existingRuvIds = [.. existingTvPrograms.Select(x => x.RuvId)];
        List<RuvProgram> removedPrograms = [.. existingTvPrograms.Where(x => !ruvIds.Contains(x.RuvId))];
        logger.LogInformation("Removing {Count} RÚV programs from database", removedPrograms.Count);

        List<RuvProgram> newPrograms = [.. programs
            .Where(x => !existingRuvIds.Contains(x.Id))
            .Select(x => RuvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes))];
        logger.LogInformation("Adding {Count} RÚV new programs to database", newPrograms.Count);

        dbContext.Set<RuvProgram>()
            .RemoveRange(removedPrograms);

        dbContext.Set<RuvProgram>()
            .AddRange(newPrograms);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}