using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Domain.Programs;
using Ruvarr.Tvdb;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TvdbLookupJob(ILogger<TvdbLookupJob> logger, RuvarrDbContext dbContext, ITvdbClient tvdb) : IJob
{
    private static readonly List<string> RomanNumerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX"];

    public async Task Execute(IJobExecutionContext context)
    {
        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes)
            .Where(x => x.TvdbId == null)
            .OrderBy(x => x.NextLookup)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null)
        {
            return;
        }

        logger.LogInformation("Processing RÚV program '{Name}'", program.Name);

        string searchText = string.IsNullOrWhiteSpace(program.ForeignName)
            ? program.Name
            : program.ForeignName;

        Datum? match = await SearchTvdbAsync(searchText).ConfigureAwait(false)
            ?? await TryRemovingRomanNumeralEnding(searchText).ConfigureAwait(false);

        if (match is null)
        {
            program.ScheduleLookup();
            _ = dbContext.SaveChangesAsync()
                .ConfigureAwait(false);

            return;
        }

        logger.LogInformation("Updating RÚV TV program with TVDB data id: '{Id}', type: '{Type}', name: '{Name}'", match.TvdbId, match.Type, match.Name);
        program.MatchTvdb(match.TvdbId, match.Type, match.Name);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private Task<Datum?> TryRemovingRomanNumeralEnding(string searchText)
    {
        string[] parts = searchText.Split(' ');

        if (!RomanNumerals.Contains(parts[^1]))
        {
            return Task.FromResult(default(Datum?));
        }

        logger.LogInformation("Roman numerals detected... trimming");
        searchText = string.Join(' ', parts[..^1]).Trim();

        return SearchTvdbAsync(searchText);
    }

    private async Task<Datum?> SearchTvdbAsync(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        logger.LogInformation("Looking up '{Name}' on the TVDB", searchText);

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

        logger.LogInformation("Tvdb returned '{Count}' exact match(es)", matches.Count);

        return matches.Count == 1 ? matches[0] : null;
    }
}