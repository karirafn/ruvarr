using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Downloads;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.DownloadEpisode;

internal sealed class DownloadEpisodeHandler(RuvarrDbContext dbContext) : IRequestHandler<DownloadEpisodeCommand>
{
    public async Task<RuvarrResult> Handle(DownloadEpisodeCommand command, CancellationToken cancellationToken)
    {
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Where(e => e.RuvId == command.ruvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            return ProgramErrors.EpisodeNotFound;
        }

        dbContext.EnqueueDownload(episode);

        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}