
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

using Ruvarr.Domain.Movies;
using Ruvarr.Domain.Programs;

using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;

namespace Ruvarr.Jobs;

[DisallowConcurrentExecution]
internal sealed class TmdbLookupJob(ILogger<TmdbLookupJob> logger, RuvarrDbContext dbContext, TMDbClient tmdb) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Where(x => !x.HasMultipleEpisodes)
            .Where(x => x.Movie == null)
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

        SearchContainer<SearchMovie>? result = await tmdb.SearchMovieAsync(searchText)
            .ConfigureAwait(false);

        if (result is null || result.Results is null)
        {
            return;
        }

        List<SearchMovie> matches = [.. result.Results.Where(x => x.Title == searchText || x.OriginalTitle == searchText)];

        if (matches is [] && result.Results.Count == 1)
        {
            Movie? movie = await tmdb.GetMovieAsync(result.Results[0].Id, MovieMethods.Translations)
                .ConfigureAwait(false);

            string? icelandicName = movie?.Translations?
                .Translations?
                .FirstOrDefault(x => x.Iso_639_1 == "is")
                ?.Data
                ?.Name;

            if (!string.IsNullOrWhiteSpace(icelandicName) && icelandicName == searchText)
            {
                matches.Add(result.Results[0]);
            }
        }

        if (matches.Count != 1)
        {
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
}