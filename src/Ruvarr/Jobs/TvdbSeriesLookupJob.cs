using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Extensions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbSeriesLookupJob(
    ILogger<TvdbSeriesLookupJob> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    TvdbSeriesLookupNotifier lookupQueue,
    TvdbEpisodeLookupNotifier episodeLookupQueue) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting Tvdb series lookup job");

        if (!lookupQueue.TryDequeue(out int ruvId))
        {
            logger.LogDebug("No RÚV program pending TVDB series lookup");
            return;
        }

        lookupQueue.MarkProcessing(ruvId);

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.RuvId == ruvId)
            .FirstOrDefaultAsync();

        if (program is null)
        {
            logger.LogDebug("No RÚV program pending TVDB series lookup");
            lookupQueue.MarkComplete(ruvId);
            return;
        }

        if (program.Series is not null)
        {
            if (!int.TryParse(program.Series.TvdbId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int tvdbId))
            {
                logger.LogWarning("Could not parse TvdbId '{TvdbId}' as integer for program '{Program}'", program.Series.TvdbId, program.Name);
                lookupQueue.MarkComplete(ruvId);
                return;
            }

            SeriesData? seriesData = await tvdb.GetSeriesAsync(tvdbId, cancellationToken: context.CancellationToken);

            if (seriesData is null)
            {
                logger.LogWarning("TVDB returned no series data for TvdbId '{TvdbId}'", tvdbId);
                lookupQueue.MarkComplete(ruvId);
                return;
            }

            program.Series.UpdateSlug(seriesData.Series.Slug);

            await dbContext.SaveChangesAsync(context.CancellationToken);

            lookupQueue.MarkComplete(ruvId);
            return;
        }

        Datum? match = await SearchTvdbAsync(program.Name, checkTranslations: true)
            ?? await TryRemovingNumeralEnding(program.Name, checkTranslations: true)
            ?? await SearchTvdbAsync(program.ForeignName, checkTranslations: false)
            ?? await TryRemovingNumeralEnding(program.ForeignName, checkTranslations: false);

        if (match is null)
        {
            await ScheduleLookupAsync(program);
            lookupQueue.MarkComplete(ruvId);
            return;
        }

        TvdbSeries? entity = await dbContext
            .Set<TvdbSeries>()
            .Where(x => x.TvdbId == match.TvdbId)
            .FirstOrDefaultAsync()
            ?? TvdbSeries.Create(match.TvdbId, match.Name, match.Slug);

        entity.UpdateSlug(match.Slug);

        program.MatchTvdb(entity);

        int added = await dbContext.SaveChangesAsync();

        lookupQueue.MarkComplete(ruvId);

        if (added > 0)
        {
            logger.LogInformation("Matched RÚV program '{Program}' with TVDB series '{Series}'", program.Name, entity.Name);
            episodeLookupQueue.Enqueue(program.RuvId, program.Name);
        }
    }

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        program.ScheduleLookup();
        logger.LogDebug(
            "TVDB returned no series matches. Next series lookup scheduled on {Timestamp}",
            program.NextLookup?.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture));

        _ = await dbContext.SaveChangesAsync();
    }

    private Task<Datum?> TryRemovingNumeralEnding(string? searchText, bool checkTranslations)
    {
        string? trimmed = searchText.WithoutNumeralEnding();

        return searchText == trimmed
            ? Task.FromResult(default(Datum?))
            : SearchTvdbAsync(trimmed, checkTranslations);
    }

    private async Task<Datum?> SearchTvdbAsync(string? searchText, bool checkTranslations)
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

        SearchResponse response = await tvdb.SearchAsync(query: sanitizedSearchText);

        List<Datum> data = [.. response.Data.Where(x => x.Type.Equals("series", StringComparison.OrdinalIgnoreCase))];
        List<Datum> matches = [];

        if (checkTranslations)
        {
            matches.AddRange(data.Where(x => x.Translations.TryGetValue("isl", out string? islName) && islName.EqualsSanitized(searchText)));
        }

        if (matches is [])
        {
            matches.AddRange(data.Where(x => x.Name.EqualsSanitized(searchText)));
        }

        logger.LogDebug("Tvdb returned '{Count}' exact match(es) for series {Name}", matches.Count, searchText);

        return matches.Count == 1 ? matches[0] : null;
    }
}