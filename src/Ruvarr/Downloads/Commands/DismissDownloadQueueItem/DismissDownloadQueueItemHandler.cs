using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Commands.DismissDownloadQueueItem;

internal sealed class DismissDownloadQueueItemHandler(RuvarrDbContext dbContext)
    : IRequestHandler<DismissDownloadQueueItemCommand>
{
    public async Task<RuvarrResult> Handle(DismissDownloadQueueItemCommand command, CancellationToken cancellationToken)
    {
        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Episode.RuvId == command.EpisodeRuvId)
            .Where(x => x.Status != DownloadQueueStatus.Complete)
            .SingleOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return DownloadErrors.ItemNotFound;
        }

        if (!item.CanDismiss)
        {
            return DownloadErrors.ItemNotDismissable;
        }

        item.Dismiss();
        dbContext.Set<DownloadQueueItem>().Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
