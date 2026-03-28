using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.MatchProgramEpisodes;

internal sealed class MatchProgramEpisodesHandler(
    ILogger<MatchProgramEpisodesHandler> logger,
    RuvarrDbContext dbcontext,
    ITvdbClient tvdb,
    ISonarrClient sonarr)
    : IRequestHandler<MatchProgramEpisodesCommand>
{
    public async Task<RuvarrResult> Handle(MatchProgramEpisodesCommand request, CancellationToken cancellationToken)
    {
        RuvProgram? program = await dbcontext.Set<RuvProgram>()
            .Where(x => x.RuvId == request.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return ProgramErrors.ProgramNotFound;
        }

        if (program.Series is null)
        {
            return ProgramErrors.ProgramNotMatched;
        }

        SeriesData? series = await tvdb.GetSeriesAsync(program.Series.TvdbId, cancellationToken);

        if (series is null)
        {
            return TvdbErrors.SeriesNotFound;
        }

        int season = program.ResolveMatchingSeason(series.Episodes);

        if (season == 0)
        {
            return ProgramErrors.SeasonUndetermined;
        }

        Dictionary<int, Episode> episodes = series.Episodes
            .Where(x => x.SeasonNumber == season)
            .ToDictionary(x => x.Number);

        if (episodes.Count != program.Episodes.Count)
        {
            return ProgramErrors.ProgramEpisodeCountMismatch;
        }

        HashSet<int> missingTvdbIds = await sonarr.GetMissingTvdbIdsAsync(cancellationToken);

        foreach (RuvEpisode episode in program.Episodes)
        {
            if (!episode.TryGetEpisodeNumber(out int number))
            {
                return ProgramErrors.UnparsableEpisodeTitle;
            }

            if (!episodes.TryGetValue(number, out Episode? tvdbEpisode))
            {
                return ProgramErrors.EpisodeNotFound;
            }

            episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, missingTvdbIds.Contains(tvdbEpisode.Id), tvdbEpisode.NameTranslations?.Contains("isl") ?? false);
            logger.LogInformation(
                "Matched RÚV episode {Episode} with TVDB episode '{TvdbSeries}' - '{TvdbEpisodeName}'",
                episode.ToString(),
                series.Series.Name,
                tvdbEpisode.Name);
        }

        await dbcontext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}