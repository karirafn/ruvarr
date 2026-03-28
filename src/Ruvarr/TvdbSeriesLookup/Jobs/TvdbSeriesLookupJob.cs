using System.Globalization;

using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Extensions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr.TvdbSeriesLookup.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbSeriesLookupJob(
    ILogger<TvdbSeriesLookupJob> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    TvdbSeriesLookupNotifier lookupQueue,
    IDomainEventBroadcaster broadcaster,
    ISettingsStore settingsStore) : IJob
{
    private const int MaxDegreeOfParallelism = 3;

    public async Task Execute(IJobExecutionContext context)
    {
        if (!IsTvdbConfigured())
        {
            return;
        }

        logger.LogDebug("Starting Tvdb series lookup job");

        if (!lookupQueue.TryDequeue(out int ruvId))
        {
            logger.LogDebug("No RÚV program pending TVDB series lookup");
            return;
        }

        lookupQueue.MarkProcessing(ruvId);
        broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());

        try
        {
            RuvProgram? program = await dbContext.Set<RuvProgram>()
                .Include(x => x.Series)
                .Include(x => x.Episodes)
                .Where(x => x.RuvId == ruvId)
                .FirstOrDefaultAsync();

            if (program is null)
            {
                logger.LogDebug("No RÚV program pending TVDB series lookup");
                return;
            }

            if (program.Series is not null)
            {
                await RefreshSeriesSlugAsync(program, context.CancellationToken);
                return;
            }

            CancellationToken cancellationToken = context.CancellationToken;

            Datum? match = await SearchAllStrategiesAsync(program, cancellationToken);

            if (match is null)
            {
                await ScheduleLookupAsync(program, context.CancellationToken);
                return;
            }

            if (!int.TryParse(match.TvdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int matchTvdbId) || matchTvdbId < 1)
            {
                logger.LogWarning("Could not parse TvdbId '{TvdbId}' as integer for TVDB match '{Name}'", match.TvdbId, match.Name);
                program.ScheduleLookup();
                await dbContext.SaveChangesAsync(context.CancellationToken);
                return;
            }

            TvdbSeries? entity = await dbContext
                .Set<TvdbSeries>()
                .Where(x => x.TvdbId == matchTvdbId)
                .FirstOrDefaultAsync()
                ?? TvdbSeries.Create(matchTvdbId, match.Name, match.Slug);

            entity.UpdateSlug(match.Slug);

            program.MatchTvdb(entity);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation("Matched RÚV program '{Program}' with TVDB series '{Series}'", program.Name, entity.Name);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent queue items from getting stuck in Processing state
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Error during TVDB series lookup for RuvId {RuvId}", ruvId);
        }
        finally
        {
            lookupQueue.MarkComplete(ruvId);
            broadcaster.Publish(new QueueChangedEvent<TvdbSeriesLookupQueueItemSummary>());
        }
    }

    private bool IsTvdbConfigured()
    {
        if (!settingsStore.Current.IsTvdbConfigured)
        {
            logger.LogDebug("Skipping {JobName}: TVDB is not configured", nameof(TvdbSeriesLookupJob));
            return false;
        }

        return true;
    }

    private async Task RefreshSeriesSlugAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        SeriesData? seriesData = await tvdb.GetSeriesAsync(program.Series!.TvdbId, cancellationToken: cancellationToken);

        if (seriesData is null)
        {
            logger.LogWarning("TVDB returned no series data for TvdbId '{TvdbId}'", program.Series.TvdbId);
            return;
        }

        program.Series.UpdateSlug(seriesData.Series.Slug);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Datum?> SearchAllStrategiesAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        IReadOnlyList<RuvEpisode> episodes = program.Episodes;

        return await SearchTvdbAsync(program.Name, checkTranslations: true, episodes: episodes, cancellationToken)
            ?? await TryRemovingNumeralEnding(program.Name, checkTranslations: true, episodes: episodes, cancellationToken)
            ?? await SearchTvdbAsync(program.ForeignName, checkTranslations: false, episodes: episodes, cancellationToken)
            ?? await TryRemovingNumeralEnding(program.ForeignName, checkTranslations: false, episodes: episodes, cancellationToken);
    }

    private async Task ScheduleLookupAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        program.ScheduleLookup();
        logger.LogDebug(
            "TVDB returned no series matches. Next series lookup scheduled on {Timestamp}",
            program.NextLookup?.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture));

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool HasYearSuffixedVariant(List<Datum> data, string searchText) => data.Any(d =>
        (d.Name.HasYearSuffix() && d.Name.WithoutYearSuffix()!.EqualsSanitized(searchText))
        || (d.Translations.TryGetValue("isl", out string? islName) && islName.HasYearSuffix() && islName.WithoutYearSuffix()!.EqualsSanitized(searchText)));

    private Task<Datum?> TryRemovingNumeralEnding(string? searchText, bool checkTranslations, IReadOnlyList<RuvEpisode> episodes, CancellationToken cancellationToken)
    {
        string? trimmed = searchText.WithoutNumeralEnding();

        return searchText == trimmed
            ? Task.FromResult(default(Datum?))
            : SearchTvdbAsync(trimmed, checkTranslations, episodes, cancellationToken);
    }

    private async Task<Datum?> SearchTvdbAsync(string? searchText, bool checkTranslations, IReadOnlyList<RuvEpisode> episodes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        string sanitizedSearchText = searchText
            .Normalize(System.Text.NormalizationForm.FormD)
            .Replace(':', ' ')
            .Replace('-', ' ')
            .Replace('!', ' ')
            .ToUpperInvariant();

        logger.LogDebug("Searching TVDB for series '{Name}'", searchText);

        SearchResponse response = await tvdb.SearchAsync(query: sanitizedSearchText, cancellationToken: cancellationToken);

        List<Datum> data = [.. response.Data.Where(x => x.Type.Equals("series", StringComparison.OrdinalIgnoreCase))];
        List<Datum> matches = [];

        if (checkTranslations)
        {
            matches.AddRange(data.Where(x => x.Translations.TryGetValue("isl", out string? islName) && islName.EqualsSanitized(searchText)));

            HashSet<string> matchIds = [.. matches.Select(m => m.TvdbId)];
            matches.AddRange(data.Where(x =>
                !matchIds.Contains(x.TvdbId)
                && x.Translations.TryGetValue("isl", out string? islName)
                && islName.HasYearSuffix()
                && islName.WithoutYearSuffix()!.EqualsSanitized(searchText)));
        }

        if (matches is [])
        {
            matches.AddRange(data.Where(x => x.Name.EqualsSanitized(searchText)));
        }

        logger.LogDebug("Tvdb returned '{Count}' exact match(es) for series {Name}", matches.Count, searchText);

        if (matches.Count == 1 && HasYearSuffixedVariant(data, searchText))
        {
            logger.LogDebug("Skipping ambiguous match for '{Name}' — year-suffixed variant exists in TVDB results", searchText);
            return null;
        }

        if (matches.Count > 1 && episodes.Count > 0)
        {
            return await DisambiguateByEpisodeTranslationsAsync(matches, episodes, cancellationToken);
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    private async Task<Datum?> DisambiguateByEpisodeTranslationsAsync(List<Datum> candidates, IReadOnlyList<RuvEpisode> episodes, CancellationToken cancellationToken)
    {
        Datum? winner = null;

        foreach (Datum candidate in candidates)
        {
            if (!int.TryParse(candidate.TvdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int candidateTvdbId) || candidateTvdbId < 1)
            {
                continue;
            }

            SeriesData? seriesData = await tvdb.GetSeriesAsync(candidateTvdbId, cancellationToken: cancellationToken);

            if (seriesData is null)
            {
                continue;
            }

            List<Episode> translatedEpisodes = [.. seriesData.Episodes
                .Where(x => x.NameTranslations.Contains("isl"))];

            if (translatedEpisodes.Count == 0)
            {
                continue;
            }

            bool hasMatch = false;

            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                await Parallel.ForEachAsync(
                    translatedEpisodes,
                    new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism, CancellationToken = linkedCts.Token },
                    async (translatedEpisode, loopToken) =>
                    {
                        EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(translatedEpisode.Id, cancellationToken: loopToken);

                        if (translation is not null
                            && !string.IsNullOrWhiteSpace(translation.Name)
                            && episodes.Any(e => e.IsMatch(translation.Name)))
                        {
                            hasMatch = true;
                            await linkedCts.CancelAsync();
                        }
                    });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Expected when short-circuiting after finding a match
            }

            if (hasMatch)
            {
                if (winner is not null)
                {
                    logger.LogDebug("Multiple candidates matched episode translations for disambiguation — returning null");
                    return null;
                }

                winner = candidate;
            }
        }

        if (winner is not null)
        {
            logger.LogDebug("Disambiguated by episode translations — selected candidate '{Name}' (TvdbId: {TvdbId})", winner.Name, winner.TvdbId);
        }

        return winner;
    }
}
