using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Queries.GetDownloadQueue;

internal sealed class GetDownloadQueueHandler(RuvarrDbContext dbContext)
    : IRequestHandler<GetDownloadQueueQuery, IReadOnlyList<DownloadQueueItemSummary>>
{
    public async Task<IReadOnlyList<DownloadQueueItemSummary>> Handle(
        GetDownloadQueueQuery request,
        CancellationToken cancellationToken)
    {
        List<DownloadQueueItemSummary> results = await dbContext
            .Set<DownloadQueueItem>()
            .Where(x => x.Status != DownloadQueueStatus.Complete)
            .OrderBy(x =>
                x.Status == DownloadQueueStatus.Failed || x.Status == DownloadQueueStatus.Exhausted ? 0 :
                x.Status == DownloadQueueStatus.Downloading ? 1 : 2)
            .ThenByDescending(x => x.Created)
            .Select(x => new DownloadQueueItemSummary(
                x.Episode.RuvId,
                x.Episode.Program.RuvId,
                x.Episode.Program.Name,
                x.Episode.Title,
                x.Status,
                x.FailureReason,
                x.RetryCount,
                x.NextRetryAt,
                x.Created))
            .ToListAsync(cancellationToken);

        return results;
    }
}
