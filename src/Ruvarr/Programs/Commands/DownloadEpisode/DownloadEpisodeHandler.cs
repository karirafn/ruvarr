using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.DownloadEpisode;

internal sealed class DownloadEpisodeHandler(RuvarrDbContext dbContext) : IRequestHandler<DownloadEpisodeCommand>
{
    private const int SqliteUniqueConstraintErrorCode = 2067;

    public async Task<RuvarrResult> Handle(DownloadEpisodeCommand command, CancellationToken cancellationToken)
    {
        RuvEpisode? episode = await dbContext.Set<RuvEpisode>()
            .Where(e => e.RuvId == command.ruvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (episode is null)
        {
            return ProgramErrors.EpisodeNotFound;
        }

        episode.RequestDownload();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintErrorCode })
        {
            // Another writer queued the episode between our check and insert, so it is already queued.
            return RuvarrResult.Success;
        }

        return RuvarrResult.Success;
    }
}
