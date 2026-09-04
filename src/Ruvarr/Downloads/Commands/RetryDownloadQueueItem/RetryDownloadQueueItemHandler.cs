using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Commands.RetryDownloadQueueItem;

internal sealed class RetryDownloadQueueItemHandler(RuvarrDbContext dbContext)
    : IRequestHandler<RetryDownloadQueueItemCommand>
{
    public async Task<RuvarrResult> Handle(RetryDownloadQueueItemCommand command, CancellationToken cancellationToken)
    {
        DownloadQueueItem? item = await dbContext.Set<DownloadQueueItem>()
            .Where(x => x.Episode.RuvId == command.EpisodeRuvId)
            .OrderByDescending(x => x.Created)
            .FirstOrDefaultAsync(cancellationToken);

        if (item is null)
        {
            return DownloadErrors.ItemNotFound;
        }

        if (item.Status is DownloadQueueStatus.Downloading or DownloadQueueStatus.Pending or DownloadQueueStatus.Complete)
        {
            return DownloadErrors.ItemNotRetryable;
        }

        item.RetryNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        return RuvarrResult.Success;
    }
}
