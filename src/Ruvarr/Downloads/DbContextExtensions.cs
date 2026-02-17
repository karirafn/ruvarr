using Microsoft.EntityFrameworkCore;

using Ruvarr.Downloads.Domain;
using Ruvarr.Ruv.Domain;

namespace Ruvarr.Downloads;

internal static class DbContextExtensions
{
    internal static void EnqueueDownload(this DbContext dbContext, RuvEpisode episode) =>
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
}