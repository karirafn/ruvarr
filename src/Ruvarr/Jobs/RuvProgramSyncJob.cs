using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Domain.Programs;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

internal sealed class RuvProgramSyncJob(IRuvClient ruv, RuvarrDbContext dbContext) : IJob
{
    private static readonly string[] ExcludedChannels = ["Fréttastofa sjónvarps", "Íþróttadeild", "Rás 1", "Rás 2"];

    public async Task Execute(IJobExecutionContext context)
    {
        RuvFeaturedTv? kids = await ruv.GetKidsTvAsync()
            .ConfigureAwait(false);

        RuvFeaturedTv? featured = await ruv.GetFeaturedTv()
            .ConfigureAwait(false);

        IReadOnlyList<RuvTvProgram> featuredPrograms = featured?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        IReadOnlyList<RuvTvProgram> kidsPograms = kids?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        IReadOnlyList<RuvTvProgram> allPrograms = [.. featuredPrograms, .. kidsPograms];

        List<RuvTvProgram> programs = [.. allPrograms
            .Where(x => !ExcludedChannels.Contains(x.Channel))
            .Where(x => x.WebAvailableEpisodes > 0)
            .DistinctBy(x => x.Id)];

        List<int> ruvIds = [.. programs.Select(x => x.Id)];

        if (ruvIds is [])
        {
            return;
        }

        List<RuvProgram> existingTvPrograms = await dbContext.Set<RuvProgram>()
            .Where(x => ruvIds.Contains(x.RuvId))
            .ToListAsync()
            .ConfigureAwait(false);

        List<int> existingRuvIds = [.. existingTvPrograms.Select(x => x.RuvId)];
        List<RuvProgram> newPrograms = [.. programs
            .Where(x => !existingRuvIds.Contains(x.Id))
            .Select(x => RuvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes))];

        dbContext.Set<RuvProgram>()
            .AddRange(newPrograms);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}