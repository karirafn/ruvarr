using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Ruvarr.Abstractions;
using Ruvarr.RomanNumerals;
using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Ruv.Commands.MatchProgramEpisodes;

internal sealed class MatchProgramEpisodesHandler(
    ILogger<MatchProgramEpisodesHandler> logger,
    RuvarrDbContext dbcontext,
    ITvdbClient tvdb)
    : IRequestHandler<MatchProgramEpisodesCommand>
{
    public async Task<RuvarrResult> Handle(MatchProgramEpisodesCommand request, CancellationToken cancellationToken)
    {
        RuvProgram? program = await dbcontext.Set<RuvProgram>()
            .Where(x => x.RuvId == request.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return RuvErrors.ProgramNotFound;
        }

        if (program.Series is null)
        {
            return RuvErrors.ProgramNotMatched;
        }

        SeriesData? series = await tvdb.GetSeriesAsync(int.Parse(program.Series.TvdbId, CultureInfo.InvariantCulture), cancellationToken);

        if (series is null)
        {
            return TvdbErrors.SeriesNotFound;
        }

        IEnumerable<Episode> episodesEnumerable = series.Episodes
            .Where(x => x.SeasonNumber > 0);

        int season = GetSeason(program, episodesEnumerable);

        if (season == 0)
        {
            return RuvErrors.SeasonUndetermined;
        }

        Dictionary<int, Episode> episodes = episodesEnumerable
            .Where(x => x.SeasonNumber == season)
            .ToDictionary(x => x.Number);

        if (episodes.Count != program.Episodes.Count)
        {
            return RuvErrors.ProgramEpisodeCountMismatch;
        }

        foreach (RuvEpisode episode in program.Episodes)
        {
            string[] parts = episode.Title.Split(' ');
            if (!parts[0].Equals("þáttur", StringComparison.OrdinalIgnoreCase) || !int.TryParse(parts[1], out int number))
            {
                return RuvErrors.UnparsableEpisodeTitle;
            }

            Episode tvdbEpisode = episodes[number];

            logger.LogInformation(
                "Matched RÚV episode '{RuvEpisode}' of program '{ProgramName}' with TVDB episode '{SeriesName}' S{Season:D2}E{Episode:D2} '{EpisodeName}'",
                episode.Title,
                program.Name,
                series.Series.Name,
                tvdbEpisode.SeasonNumber,
                tvdbEpisode.Number,
                tvdbEpisode.Name);
            episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number);
        }

        await dbcontext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }

    private static int GetSeason(RuvProgram program, IEnumerable<Episode> episodes)
    {
        if (RomanNumeral.TryParse(program.Name.Split(' ')[^1], out RomanNumeral? romanNumeral))
        {
            return romanNumeral.Number;
        }

        Dictionary<int, int> episodesPerSeason = episodes
            .GroupBy(x => x.SeasonNumber)
            .Select(x => (Season: x.Key, Count: x.Count()))
            .Where(x => x.Count == program.Episodes.Count)
            .ToDictionary(x => x.Season, x => x.Count);

        return episodesPerSeason.Count == 1 ? episodesPerSeason.Single().Key : 0;
    }
}