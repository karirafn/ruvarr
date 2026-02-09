using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Domain.TvProgram;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Models;

namespace Ruvarr.Jobs;

internal sealed class RuvSeriesSyncJob(IRuvClient ruv, RuvarrDbContext dbContext) : IJob
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

        List<TvProgram> existingTvPrograms = await dbContext.Set<TvProgram>()
            .Where(x => ruvIds.Contains(x.RuvId))
            .ToListAsync()
            .ConfigureAwait(false);

        List<int> existingRuvIds = [.. existingTvPrograms.Select(x => x.RuvId)];
        List<TvProgram> newPrograms = [.. programs
            .Where(x => !existingRuvIds.Contains(x.Id))
            .Select(x => TvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes))];

        dbContext.Set<TvProgram>()
            .AddRange(newPrograms);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}