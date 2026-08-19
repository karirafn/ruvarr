using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.Jobs;

internal sealed class RuvProgramRefreshJob(
    ILogger<RuvProgramRefreshJob> logger,
    IRuvClient ruv,
    RuvarrDbContext dbContext,
    ISettingsStore settingsStore,
    ProgramRefreshNotifier syncQueue,
    TvdbSeriesLookupNotifier tvdbLookupQueue,
    IDomainEventBroadcaster broadcaster) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting RÚV programs refresh job");

        CancellationToken cancellationToken = context.CancellationToken;

        List<RuvTvProgram> programs = await FetchRuvPrograms(cancellationToken);

        if (programs is [])
        {
            return;
        }

        HashSet<int> apiRuvIds = [.. programs.Select(x => x.Id)];

        List<RuvProgram> existingTvPrograms = await SyncPrograms(programs, cancellationToken);

        UpdateExistingProgramMetadata(existingTvPrograms, programs);

        await dbContext.SaveChangesAsync(cancellationToken);

        EnqueueProgramRefreshes(programs);

        await EnqueueKnownProgramRefreshes(apiRuvIds, cancellationToken);

        await EnqueueUnmatchedProgramsForLookup(cancellationToken);

        await EnqueueSlugMissingProgramsForLookup(cancellationToken);

        broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());
    }

    private async Task<List<RuvTvProgram>> FetchRuvPrograms(CancellationToken cancellationToken)
    {
        RuvFeaturedTv? kidsCategory = await ruv.GetKidsTvAsync(cancellationToken);
        List<RuvTvProgram> kidsPrograms = kidsCategory?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} Krakka RÚV programs", kidsPrograms.Count);

        RuvFeaturedTv? featured = await ruv.GetFeaturedTv(cancellationToken);
        List<RuvTvProgram> featuredPrograms = featured?.Panels.SelectMany(x => x.Programs).ToList() ?? [];
        logger.LogDebug("Found {Count} featured RÚV programs", featuredPrograms.Count);

        List<RuvTvProgram> combinedPrograms = [.. featuredPrograms, .. kidsPrograms];
        logger.LogDebug("Found {Count} total RÚV programs", combinedPrograms.Count);

        HashSet<string> ignoredPrograms = new(settingsStore.Current.IgnoredPrograms, StringComparer.OrdinalIgnoreCase);

        List<RuvTvProgram> programs = [.. combinedPrograms
            .Where(x => !settingsStore.Current.IgnoredChannels.Contains(x.Channel))
            .Where(x => !ignoredPrograms.Contains(x.Title))
            .Where(x => x.WebAvailableEpisodes > 0)
            .DistinctBy(x => x.Id)];
        logger.LogDebug("Found {Count} distinct RÚV programs", programs.Count);

        return programs;
    }

    private async Task<List<RuvProgram>> SyncPrograms(List<RuvTvProgram> programs, CancellationToken cancellationToken)
    {
        List<int> ruvIds = [.. programs.Select(x => x.Id)];

        List<RuvProgram> existingTvPrograms = await dbContext.Set<RuvProgram>()
            .IgnoreAutoIncludes()
            .Where(x => ruvIds.Contains(x.RuvId))
            .ToListAsync(cancellationToken);
        logger.LogDebug("Found {Count} RÚV programs in database", existingTvPrograms.Count);

        List<int> existingRuvIds = [.. existingTvPrograms.Select(x => x.RuvId)];
        List<RuvProgram> removedPrograms = [.. existingTvPrograms.Where(x => !ruvIds.Contains(x.RuvId))];

        if (removedPrograms.Count > 0)
        {
            logger.LogInformation("Removing {Count} RÚV programs from database", removedPrograms.Count);
        }

        List<RuvProgram> newPrograms = [.. programs
            .Where(x => !existingRuvIds.Contains(x.Id))
            .Select(x => RuvProgram.Create(x.Id, x.Channel, x.Title, x.ForeignTitle, x.MultipleEpisodes, x.Slug, x.Image, JoinDescription(x.Description)))];

        if (newPrograms.Count > 0)
        {
            logger.LogInformation("Adding {Count} RÚV new programs to database", newPrograms.Count);
        }

        dbContext.Set<RuvProgram>()
            .RemoveRange(removedPrograms);

        dbContext.Set<RuvProgram>()
            .AddRange(newPrograms);

        return existingTvPrograms;
    }

    private static void UpdateExistingProgramMetadata(List<RuvProgram> existingTvPrograms, List<RuvTvProgram> programs)
    {
        Dictionary<int, string> slugByRuvId = programs
            .Where(x => x.Slug is not null)
            .ToDictionary(x => x.Id, x => x.Slug);

        Dictionary<int, Uri?> imageByRuvId = programs.ToDictionary(x => x.Id, x => x.Image);
        Dictionary<int, string?> descriptionByRuvId = programs.ToDictionary(x => x.Id, x => JoinDescription(x.Description));
        Dictionary<int, bool> multipleEpisodesByRuvId = programs.ToDictionary(x => x.Id, x => x.MultipleEpisodes);

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

            if (descriptionByRuvId.TryGetValue(existing.RuvId, out string? incomingDescription) && existing.Description != incomingDescription)
            {
                existing.UpdateDescription(incomingDescription);
            }

            if (multipleEpisodesByRuvId.TryGetValue(existing.RuvId, out bool incomingMultipleEpisodes))
            {
                existing.UpdateHasMultipleEpisodes(incomingMultipleEpisodes);
            }
        }
    }

    private void EnqueueProgramRefreshes(List<RuvTvProgram> programs)
    {
#pragma warning disable CA1309 // Culture-sensitive comparison is intentional for Icelandic alphabetical ordering
        programs.Sort((a, b) => string.Compare(a.Title, b.Title, new CultureInfo("is-IS"), CompareOptions.None));
#pragma warning restore CA1309

        foreach (RuvTvProgram program in programs.Where(x => x.MultipleEpisodes))
        {
            syncQueue.Enqueue(program.Id, program.Title);
        }
        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }

    private async Task EnqueueKnownProgramRefreshes(HashSet<int> apiRuvIds, CancellationToken cancellationToken)
    {
        List<RuvProgram> knownPrograms = await dbContext.Set<RuvProgram>()
            .IgnoreAutoIncludes()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => !apiRuvIds.Contains(x.RuvId))
            .ToListAsync(cancellationToken);

        foreach (RuvProgram program in knownPrograms)
        {
            syncQueue.Enqueue(program.RuvId, program.Name);
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());
    }

    private async Task EnqueueUnmatchedProgramsForLookup(CancellationToken cancellationToken)
    {
        List<RuvProgram> unmatchedPrograms = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series == null)
            .Where(x => x.NextLookup == null || x.NextLookup <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (RuvProgram program in unmatchedPrograms)
        {
            tvdbLookupQueue.Enqueue(program.RuvId, program.Name);
        }
    }

    private async Task EnqueueSlugMissingProgramsForLookup(CancellationToken cancellationToken)
    {
        List<RuvProgram> slugMissingPrograms = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series != null)
            .Where(x => x.Series!.Slug == null)
            .ToListAsync(cancellationToken);

        HashSet<int> enqueuedTvdbIds = [];

        foreach (RuvProgram program in slugMissingPrograms)
        {
            if (!enqueuedTvdbIds.Add(program.Series!.TvdbId))
            {
                continue;
            }

            tvdbLookupQueue.Enqueue(program.RuvId, program.Name);
        }
    }

    private static string? JoinDescription(IReadOnlyList<string> paragraphs)
    {
        if (paragraphs is [])
        {
            return null;
        }

        string joined = string.Join("\n\n", paragraphs);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}