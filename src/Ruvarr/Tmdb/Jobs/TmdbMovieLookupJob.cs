
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
        logger.LogInformation("Starting TMDB movie lookup job");

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => !x.HasMultipleEpisodes)
            .Where(x => x.Movie == null)
            .OrderBy(x => x.NextLookup)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (program is null)
        {
            logger.LogInformation("No RÚV program pending TMDB lookup");
            return;
        }

        string searchText = string.IsNullOrWhiteSpace(program.ForeignName)
            ? program.Name
            : program.ForeignName;

        logger.LogInformation("Searching TMDB for '{Name}'", searchText);
        SearchContainer<SearchMovie>? result = await tmdb.SearchMovieAsync(searchText)
            .ConfigureAwait(false);

        if (result is null || result.Results is null)
        {
            await ScheduleLookupAsync(program).ConfigureAwait(false);
            return;
        }

        List<SearchMovie> matches = [.. result.Results
            .Where(x => x.MediaType == MediaType.Movie)
            .Where(x => x.Title == searchText || x.OriginalTitle == searchText)];

        if (matches is [] && result.Results.Count == 1)
        {
            logger.LogInformation("TMDB returned single match but title and original title did not match '{Name}'. Checking translations", searchText);
            Movie? movie = await tmdb.GetMovieAsync(result.Results[0].Id, MovieMethods.Translations)
                .ConfigureAwait(false);

            string? icelandicName = movie?.Translations?
                .Translations?
                .FirstOrDefault(x => x.Iso_639_1 == "is")
                ?.Data
                ?.Name;

            if (!string.IsNullOrWhiteSpace(icelandicName) && icelandicName == searchText)
            {
                logger.LogInformation("Matched TMDB icelandic translation '{Name}'", searchText);
                matches.Add(result.Results[0]);
            }
        }

        if (matches.Count != 1)
        {
            await ScheduleLookupAsync(program).ConfigureAwait(false);
            return;
        }

        SearchMovie match = matches[0];
        TmdbMovie entity = await dbContext.Set<TmdbMovie>()
            .Where(x => x.TmdbId == match.Id)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false)
            ?? TmdbMovie.Create(match.Id, match.Title ?? match.OriginalTitle ?? string.Empty);

        program.MatchTmdb(entity);

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }

    private async Task ScheduleLookupAsync(RuvProgram program)
    {
        program.ScheduleLookup();
        logger.LogInformation(
            "TMDB returned no matches. Next lookup scheduled on {Timestamp}",
            program.NextLookup?.ToString("yyyy-MM-dd - hh:mm", CultureInfo.InvariantCulture));

        await dbContext.SaveChangesAsync()
            .ConfigureAwait(false);
    }
}