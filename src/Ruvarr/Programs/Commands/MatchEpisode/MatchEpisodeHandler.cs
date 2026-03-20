using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
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

        logger.LogDebug("Matching episode with Ruv ID {RuvId}", command.RuvId);
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Include(x => x.Program)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            logger.LogWarning("RÚV episode with Ruv ID {RuvId} not found", command.RuvId);
            return ProgramErrors.EpisodeNotFound;
        }

        Episode? tvdbEpisode = await tvdb.GetEpisodeAsync(command.TvdbId, cancellationToken);
        if (tvdbEpisode is null)
        {
            logger.LogWarning("TVDB episode with TVDB id {TvdbId} not found", command.TvdbId);
            return TvdbErrors.EpisodeNotFound;
        }

        IReadOnlyCollection<MissingEpisode> missingEpisodes = await sonarr.GetMissingEpisodesAsync(cancellationToken: cancellationToken);
        bool isMissing = missingEpisodes.Any(x => x.TvdbId == tvdbEpisode.Id);

        episode.Match(tvdbEpisode.Id, tvdbEpisode.SeasonNumber, tvdbEpisode.Number, isMissing);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Matched RÚV episode {Episode} with TVDB episode {TvdbEpisodeName}",
            episode.ToString(),
            tvdbEpisode.Name);

        return RuvarrResult.Success;
    }
}