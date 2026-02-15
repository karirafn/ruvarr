using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb.Domain;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Tvdb.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbSeriesLookupJob(ILogger<TvdbSeriesLookupJob> logger, RuvarrDbContext dbContext, ITvdbClient tvdb) : IJob
{
    private static readonly List<string> RomanNumerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"];

    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting Tvdb series lookup job");

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.Series == null)
            .OrderBy(x => x.NextLookup)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null)
        {
            logger.LogDebug("No RÚV program pending TVDB series lookup");
            return;
        }

        string searchText = string.IsNullOrWhiteSpace(program.ForeignName)
            ? program.Name
            : program.ForeignName;

        Datum? match = await SearchTvdbAsync(searchText).ConfigureAwait(false)
            ?? await TryRemovingRomanNumeralEnding(searchText).ConfigureAwait(false);

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

    private Task<Datum?> TryRemovingRomanNumeralEnding(string searchText)
    {
        string[] parts = searchText.Split(' ');

        if (!RomanNumerals.Contains(parts[^1]))
        {
            return Task.FromResult(default(Datum?));
        }

        logger.LogDebug("Roman numerals detected in series name... trimming");
        searchText = string.Join(' ', parts[..^1]).Trim();

        return SearchTvdbAsync(searchText);
    }

    private async Task<Datum?> SearchTvdbAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        logger.LogDebug("Searching TVDB for series '{Name}'", searchText);

        SearchResponse response = await tvdb.SearchAsync(query: searchText)
            .ConfigureAwait(false);

        List<Datum> data = [.. response.Data
            .Where(x => x.Type == "series")
            .Select(x => x with
        {
            Name = x.Name
                // Remove soft hyphens (https://en.wikipedia.org/wiki/Soft_hyphen)
                .Replace("\u00AD", string.Empty, StringComparison.OrdinalIgnoreCase)
        })];

        List<Datum> matches = [.. data.Where(x => x.Name.Equals(searchText, StringComparison.OrdinalIgnoreCase))];

        if (matches is [])
        {
            matches = [.. data.Where(x => x.Translations.TryGetValue("isl", out string? islName) && islName.Equals(searchText, StringComparison.OrdinalIgnoreCase))];
        }

        logger.LogDebug("Tvdb returned '{Count}' exact match(es) for series {Name}", matches.Count, searchText);

        return matches.Count == 1 ? matches[0] : null;
    }
}