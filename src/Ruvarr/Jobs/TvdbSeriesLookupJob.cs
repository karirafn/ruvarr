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

        Datum? match = await SearchTvdbAsync(program.Name, checkTranslations: true).ConfigureAwait(false)
            ?? await TryRemovingNumeralEnding(program.Name, checkTranslations: true).ConfigureAwait(false)
            ?? await SearchTvdbAsync(program.ForeignName, checkTranslations: false).ConfigureAwait(false)
            ?? await TryRemovingNumeralEnding(program.ForeignName, checkTranslations: false).ConfigureAwait(false);

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
            .Replace('!', ' ');

        logger.LogDebug("Searching TVDB for series '{Name}'", searchText);

        SearchResponse response = await tvdb.SearchAsync(query: sanitizedSearchText, type: "series")
            .ConfigureAwait(false);

        List<Datum> matches = [];

        if (checkTranslations)
        {
            matches.AddRange(response.Data.Where(x => x.Translations.TryGetValue("isl", out string? islName) && islName.EqualsSanitized(searchText)));
        }

        if (matches is [])
        {
            matches.AddRange(response.Data.Where(x => x.Name.EqualsSanitized(searchText)));
        }

        logger.LogDebug("Tvdb returned '{Count}' exact match(es) for series {Name}", matches.Count, searchText);

        return matches.Count == 1 ? matches[0] : null;
    }
}