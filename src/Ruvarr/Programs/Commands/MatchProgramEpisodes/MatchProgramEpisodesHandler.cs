using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
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

        SeriesData? series = await tvdb.GetSeriesAsync(int.Parse(program.Series.TvdbId, CultureInfo.InvariantCulture), cancellationToken);

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

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);
        HashSet<int> missingTvdbIds = [.. missingEpisodes.Select(x => x.TvdbId)];

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

            logger.LogInformation(
                "Matched RÚV episode '{RuvEpisode}' of program '{ProgramName}' with TVDB episode '{SeriesName}' S{Season:D2}E{Episode:D2} '{EpisodeName}'",
                episode.Title,
                program.Name,
                series.Series.Name,
                tvdbEpisode.SeasonNumber,
                tvdbEpisode.Number,
                tvdbEpisode.Name);
            episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, missingTvdbIds.Contains(tvdbEpisode.Id));
        }

        await dbcontext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}