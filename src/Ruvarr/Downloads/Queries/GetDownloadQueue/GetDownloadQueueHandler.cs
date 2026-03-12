using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Queries.GetDownloadQueue;

internal sealed class GetDownloadQueueHandler(RuvarrDbContext dbContext)
    : IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>
{
    public async Task<List<DownloadQueueItemSummary>> Handle(GetDownloadQueueQuery request, CancellationToken cancellationToken)
    {
        IQueryable<DownloadQueueItem> query = dbContext.Set<DownloadQueueItem>();

        if (!request.IncludeDownloaded)
        {
            query = query.Where(x => x.Status != DownloadQueueStatus.Complete);
        }

        return await query
            .OrderBy(x => x.Downloaded.HasValue)
            .ThenByDescending(x => x.Created)
            .Select(x => new DownloadQueueItemSummary(
                x.Episode.RuvId,
                x.Episode.Title,
                x.Episode.Program.Name,
                x.Created,
                x.Downloaded,
                x.Status))
            .ToListAsync(cancellationToken);
    }
}
