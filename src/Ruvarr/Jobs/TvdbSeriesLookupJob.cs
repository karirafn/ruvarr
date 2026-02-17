using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Extensions;
using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb;
using Ruvarr.Tvdb.Domain;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbSeriesLookupJob(ILogger<TvdbSeriesLookupJob> logger, RuvarrDbContext dbContext, ITvdbClient tvdb) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting Tvdb series lookup job");

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series == null)
            .Where(x => x.NextLookup == null || x.NextLookup <= DateTime.UtcNow)
            .OrderBy(x => x.NextLookup)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null)
        {
            logger.LogDebug("No RÚV program pending TVDB series lookup");
            return;
        }

        Datum? match = await SearchTvdbAsync(program.ForeignName, checkTranslations: false).ConfigureAwait(false)
            ?? await TryRemovingRomanNumeralEnding(program.ForeignName, checkTranslations: false).ConfigureAwait(false)
            ?? await SearchTvdbAsync(program.Name, checkTranslations: true).ConfigureAwait(false)
            ?? await TryRemovingRomanNumeralEnding(program.Name, checkTranslations: true).ConfigureAwait(false);

        if (match is null)
        {
            await ScheduleLookupAsync(program).ConfigureAwait(false);
            return;
        }

        TvdbSeries? entity = await dbContext
            .Set<TvdbSeries>()
            .Where(x => x.TvdbId == match.TvdbId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false)
            ?? TvdbSeries.Create(match.TvdbId, match.Type, match.Name);

        program.MatchTvdb(entity);

        int added = await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);

        if (added > 0)
        {
            logger.LogInformation("Matched RÚV program '{Program}' with TVDB series '{Series}'", program.Name, entity.Name);
        }
    }

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        program.ScheduleLookup();
        logger.LogDebug(
            "TVDB returned no series matches. Next series lookup scheduled on {Timestamp}",
            program.NextLookup?.ToString("yyyy-MM-dd hh:mm:ss", CultureInfo.InvariantCulture));

        _ = await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private Task<Datum?> TryRemovingRomanNumeralEnding(string? searchText, bool checkTranslations)
    {
        string? trimmed = searchText.WithoutRomanNumeralEnding();

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

        searchText = searchText
            .Replace(':', ' ')
            .Replace('-', ' ')
            .Replace('!', ' ');

        logger.LogDebug("Searching TVDB for series '{Name}'", searchText);

        SearchResponse response = await tvdb.SearchAsync(query: searchText)
            .ConfigureAwait(false);

        List<Datum> data = [.. response.Data.Where(x => x.Type == "series")];

        List<Datum> matches = checkTranslations
            ? [.. data.Where(x => x.Translations.TryGetValue("isl", out string? islName) && islName.EqualsSanitized(searchText))]
            : [.. data.Where(x => x.Name.EqualsSanitized(searchText))];

        logger.LogDebug("Tvdb returned '{Count}' exact match(es) for series {Name}", matches.Count, searchText);

        return matches.Count == 1 ? matches[0] : null;
    }
}