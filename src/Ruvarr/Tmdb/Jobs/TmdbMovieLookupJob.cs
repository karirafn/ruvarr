
using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Ruv.Domain;
using Ruvarr.Tmdb.Domain;

using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;

namespace Ruvarr.Tmdb.Jobs;

[DisallowConcurrentExecution]
internal sealed class TmdbMovieLookupJob(ILogger<TmdbMovieLookupJob> logger, RuvarrDbContext dbContext, TMDbClient tmdb) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogDebug("Starting TMDB movie lookup job");

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => !x.HasMultipleEpisodes)
            .Where(x => x.Movie == null)
            .Where(x => x.NextLookup == null || x.NextLookup <= DateTime.UtcNow)
            .OrderBy(x => x.NextLookup)
            .FirstOrDefaultAsync();

        if (program is null)
        {
            logger.LogDebug("No RÚV program pending TMDB lookup");
            return;
        }

        string searchText = string.IsNullOrWhiteSpace(program.ForeignName)
            ? program.Name
            : program.ForeignName;

        logger.LogDebug("Searching TMDB for '{Name}'", searchText);
        SearchContainer<SearchMovie>? result = await tmdb.SearchMovieAsync(searchText);

        if (result is null || result.Results is null)
        {
            await ScheduleLookupAsync(program);
            return;
        }

        List<SearchMovie> matches = [.. result.Results
            .Where(x => x.MediaType == MediaType.Movie)
            .Where(x => x.Title == searchText || x.OriginalTitle == searchText)];

        if (matches is [] && result.Results.Count == 1)
        {
            logger.LogDebug("TMDB returned single match but title and original title did not match '{Name}'. Checking translations", searchText);
            Movie? movie = await tmdb.GetMovieAsync(result.Results[0].Id, MovieMethods.Translations);

            string? icelandicName = movie?.Translations?
                .Translations?
                .FirstOrDefault(x => x.Iso_639_1 == "is")
                ?.Data
                ?.Name;

            if (!string.IsNullOrWhiteSpace(icelandicName) && icelandicName == searchText)
            {
                logger.LogDebug("Matched TMDB icelandic translation '{Name}'", searchText);
                matches.Add(result.Results[0]);
            }
        }

        if (matches.Count != 1)
        {
            await ScheduleLookupAsync(program);
            return;
        }

        SearchMovie match = matches[0];
        TmdbMovie entity = await dbContext.Set<TmdbMovie>()
            .Where(x => x.TmdbId == match.Id)
            .FirstOrDefaultAsync()
            ?? TmdbMovie.Create(match.Id, match.Title ?? match.OriginalTitle ?? string.Empty);

        program.MatchTmdb(entity);

        int added = await dbContext.SaveChangesAsync();

        if (added > 0)
        {
            logger.LogInformation("Matched RÚV program '{Program}' with movie '{Movie}'", program.Name, match.Title);
        }
    }

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        program.ScheduleLookup();
        logger.LogDebug(
            "TMDB returned no matches. Next lookup scheduled on {Timestamp}",
            program.NextLookup?.ToString("yyyy-MM-dd - hh:mm", CultureInfo.InvariantCulture));

        await dbContext.SaveChangesAsync();
    }
}