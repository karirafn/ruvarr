using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Ruvarr.Abstractions;
using Ruvarr.Ruv.Domain;
using Ruvarr.Tvdb;
using Ruvarr.Tvdb.Models;

namespace Ruvarr.Ruv.Commands.MatchEpisode;

internal sealed class MatchEpisodeHandler(
    ILogger<MatchEpisodeHandler> logger,
    RuvarrDbContext dbContext,
    ITvdbClient tvdb) : IRequestHandler<MatchEpisodeCommand>
{
    public async Task<RuvarrResult> Handle(MatchEpisodeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        logger.LogDebug("Matching episode with Ruv ID {RuvId}", command.RuvId);
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            logger.LogWarning("RÚV episode with Ruv ID {RuvId} not found", command.RuvId);
            return RuvErrors.EpisodeNotFound;
        }

        Episode? tvdbEpisode = await tvdb.GetEpisodeAsync(command.TvdbId, cancellationToken);
        if (tvdbEpisode is null)
        {
            logger.LogWarning("TVDB episode with TVDB id {TvdbId} not found", command.TvdbId);
            return TvdbErrors.EpisodeNotFound;
        }

        episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Matched RÚV episode {Program} - {Title} with TVDB S{Season:D2}E{Number:D2} - {Name}",
            episode.Program.Name,
            episode.Title,
            tvdbEpisode.SeasonNumber,
            tvdbEpisode.Number,
            tvdbEpisode.Name);

        return RuvarrResult.Success;
    }
}