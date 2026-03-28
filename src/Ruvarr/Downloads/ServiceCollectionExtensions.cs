using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Commands.DeleteDownloadQueueItem;
using Ruvarr.Downloads.Events;
using Ruvarr.Downloads.Queries.GetDownloadQueue;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDownloads(this IServiceCollection services)
    {
        services.AddTransient<IRequestHandler<DeleteDownloadQueueItemCommand>, DeleteDownloadQueueItemHandler>();
        services.AddTransient<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>, GetDownloadQueueHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeMissingEvent>, EpisodeMissingEventHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeDownloadRequestedEvent>, EpisodeDownloadRequestedEventHandler>();
        services.AddTransient<IDomainEventHandler<DownloadStartedEvent>, BroadcastEventHandler<DownloadStartedEvent>>();
        services.AddTransient<IDomainEventHandler<DownloadCompletedEvent>, BroadcastEventHandler<DownloadCompletedEvent>>();

        return services;
    }
}