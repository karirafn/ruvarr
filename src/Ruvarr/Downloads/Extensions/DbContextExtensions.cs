using Microsoft.EntityFrameworkCore;

using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Downloads.Extensions;

internal static class DbContextExtensions
{
    internal static void EnqueueDownload(this DbContext dbContext, RuvEpisode episode)
    {
        if (episode.DownloadQueueItem != null)
        {
            return;
        }

        dbContext.Set<DownloadQueueItem>()
            .Add(DownloadQueueItem.Create(episode));
    }
}
