using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Downloads;
using Ruvarr.Ruv.Domain;

namespace Ruvarr.Ruv.Commands.DownloadEpisode;

public sealed class DownloadEpisodeHandler(RuvarrDbContext dbContext)
{
    public async Task<RuvarrResult> Handle(DownloadEpisodeCommand command, CancellationToken cancellationToken = default)
    {
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Where(e => e.RuvId == command.ruvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            return RuvErrors.EpisodeNotFound;
        }

        dbContext.EnqueueDownload(episode);

        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}