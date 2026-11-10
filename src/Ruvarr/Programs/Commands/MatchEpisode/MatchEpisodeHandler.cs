using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.MatchEpisode;

internal sealed class MatchEpisodeHandler(
    ILogger<MatchEpisodeHandler> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb,
    ISonarrClient sonarr) : IRequestHandler<MatchEpisodeCommand>
{
    public async Task<RuvarrResult> Handle(MatchEpisodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.TvdbIds.Count > 10)
        {
            return ProgramErrors.TooManyTvdbIds;
        }

        logger.LogDebug("Matching episode with Ruv ID {RuvId}", command.RuvId);
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Include(x => x.TvdbEpisodes)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            logger.LogWarning("RÚV episode with Ruv ID {RuvId} not found", command.RuvId);
            return ProgramErrors.EpisodeNotFound;
        }

        SonarrEpisodeStatuses sonarrStatuses = await LookUpSonarrStatusesAsync(episode.Program, cancellationToken);

        List<TvdbEpisode> tvdbEpisodes = [];
        foreach (int tvdbId in command.TvdbIds)
        {
            Episode? tvdbEpisode = await tvdb.GetEpisodeAsync(tvdbId, cancellationToken);
            if (tvdbEpisode is null)
            {
                logger.LogWarning("TVDB episode with TVDB id {TvdbId} not found", tvdbId);
                return TvdbErrors.EpisodeNotFound;
            }

            tvdbEpisodes.Add(TvdbEpisode.Create(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, tvdbEpisode.NameTranslations?.Contains("isl") ?? false));
        }

        episode.MatchMultiple(tvdbEpisodes, sonarrStatuses, MatchOrigin.Manual);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Matched RÚV episode {Episode} with {Count} TVDB episode(s)",
            episode.ToString(),
            tvdbEpisodes.Count);

        return RuvarrResult.Success;
    }

    private async Task<SonarrEpisodeStatuses> LookUpSonarrStatusesAsync(RuvProgram program, CancellationToken cancellationToken)
    {
        if (program.Series is null)
        {
            // A program without a TVDB series cannot be in Sonarr.
            return SonarrEpisodeStatuses.From(new Dictionary<int, SonarrEpisodeStatus>());
        }

        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> statusResult =
            await sonarr.GetEpisodeStatusesAsync(program.Series.TvdbId, cancellationToken);

        // Linking to TVDB does not depend on Sonarr; the next healthy RÚV episode sync sets the Sonarr status.
        return statusResult.Match(
            SonarrEpisodeStatuses.From,
            error =>
            {
                logger.LogWarning(
                    "Sonarr episode status lookup failed ({ErrorCode}); keeping stored Sonarr statuses until the next episode sync",
                    error.Code);
                return SonarrEpisodeStatuses.Unavailable;
            });
    }
}