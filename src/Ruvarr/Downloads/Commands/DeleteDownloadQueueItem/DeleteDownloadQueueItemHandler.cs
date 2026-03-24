using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Commands.DeleteDownloadQueueItem;

internal sealed class DeleteDownloadQueueItemHandler(RuvarrDbContext dbContext)
    : IRequestHandler<DeleteDownloadQueueItemCommand>
{
    public async Task<RuvarrResult> Handle(DeleteDownloadQueueItemCommand command, CancellationToken cancellationToken)
    {
        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Episode.RuvId == command.EpisodeRuvId)
            .OrderByDescending(x => x.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return DownloadErrors.ItemNotFound;
        }

        if (item.Status is not (Contracts.DownloadQueueStatus.Pending or Contracts.DownloadQueueStatus.Failed))
        {
            return DownloadErrors.ItemNotDeletable;
        }

        dbContext.Set<DownloadQueueItem>().Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
