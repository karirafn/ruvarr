using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

namespace Ruvarr.Downloads;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDownloads(this IServiceCollection services)
    {
        services.AddTransient<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>, GetDownloadQueueHandler>();

        return services;
    }
}
