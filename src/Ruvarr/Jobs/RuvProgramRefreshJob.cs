using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Jobs;

internal sealed class RuvProgramRefreshJob(
    ILogger<RuvProgramRefreshJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    IOptions<RuvarrOptions> options,
    ProgramRefreshNotifier syncQueue,
    TvdbSeriesLookupNotifier tvdbLookupQueue,
    IDomainEventBroadcaster broadcaster) : IJob
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
            .Select(x => RuvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes, x.Slug, x.Image))];

        if (newPrograms.Count > 0)
        {
            logger.LogInformation("Adding {Count} RÚV new programs to database", newPrograms.Count);
        }

        dbContext.Set<RuvProgram>()
            .RemoveRange(removedPrograms);

        dbContext.Set<RuvProgram>()
            .AddRange(newPrograms);

        Dictionary<int, string> slugByRuvId = programs
            .Where(x => x.Slug is not null)
            .ToDictionary(x => x.Id, x => x.Slug);

        Dictionary<int, Uri?> imageByRuvId = programs.ToDictionary(x => x.Id, x => x.Image);

        foreach (RuvProgram existing in existingTvPrograms)
        {
            slugByRuvId.TryGetValue(existing.RuvId, out string? incomingSlug);
            if (existing.Slug != incomingSlug)
            {
                existing.UpdateSlug(incomingSlug);
            }

            if (imageByRuvId.TryGetValue(existing.RuvId, out Uri? incomingImage) && existing.ImageUrl != incomingImage)
            {
                existing.UpdateImageUrl(incomingImage);
            }
        }

        await dbContext.SaveChangesAsync();

        foreach (RuvTvProgram program in programs.Where(x => x.MultipleEpisodes))
        {
            syncQueue.Enqueue(program.Id, program.Title);
        }
        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());

        List<RuvProgram> unmatchedPrograms = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series == null)
            .Where(x => x.NextLookup == null || x.NextLookup <= DateTime.UtcNow)
            .ToListAsync();

        foreach (RuvProgram program in unmatchedPrograms)
        {
            tvdbLookupQueue.Enqueue(program.RuvId, program.Name);
        }

        List<RuvProgram> slugMissingPrograms = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series != null)
            .Where(x => x.Series!.Slug == null)
            .ToListAsync();

        HashSet<int> enqueuedTvdbIds = [];

        foreach (RuvProgram program in slugMissingPrograms)
        {
            if (!enqueuedTvdbIds.Add(program.Series!.TvdbId))
            {
                continue;
            }

            tvdbLookupQueue.Enqueue(program.RuvId, program.Name);
        }
        broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());
    }
}