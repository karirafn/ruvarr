using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Events;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Downloads.Queries.GetDownloadQueue;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDownloads(this IServiceCollection services)
    {
        services.AddSingleton<DownloadQueueNotifier>();
        services.AddTransient<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>, GetDownloadQueueHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeMissingEvent>, EpisodeMissingEventHandler>();
        services.AddTransient<IDomainEventHandler<DownloadStartedEvent>, DownloadStartedEventHandler>();
        services.AddTransient<IDomainEventHandler<DownloadCompletedEvent>, DownloadCompletedEventHandler>();

        return services;
    }
}