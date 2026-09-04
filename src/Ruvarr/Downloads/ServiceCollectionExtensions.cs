using Ruvarr.Abstractions;
using Ruvarr.Downloads.Commands.DeleteDownloadQueueItem;
using Ruvarr.Downloads.Events;
using Ruvarr.Downloads.Notifiers;
using Ruvarr.Programs.Events;

namespace Ruvarr.Downloads;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddDownloads(this IServiceCollection services)
    {
        services.AddHostedService<IncompleteDownloadCleanupService>();
        services.AddSingleton<DownloadFileStore>();
        services.AddSingleton(sp => new DownloadProgressNotifier(
            sp.GetRequiredService<IDomainEventBroadcaster>(),
            sp.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddTransient<IRequestHandler<DeleteDownloadQueueItemCommand>, DeleteDownloadQueueItemHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeMissingEvent>, EpisodeMissingEventHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeDownloadRequestedEvent>, EpisodeDownloadRequestedEventHandler>();
        services.AddTransient<IDomainEventHandler<DownloadStartedEvent>, BroadcastEventHandler<DownloadStartedEvent>>();
        services.AddTransient<IDomainEventHandler<DownloadCompletedEvent>, BroadcastEventHandler<DownloadCompletedEvent>>();
        services.AddTransient<IDomainEventHandler<DownloadFailedEvent>, BroadcastEventHandler<DownloadFailedEvent>>();
        services.AddTransient<IDomainEventHandler<DownloadProgressUpdatedEvent>, BroadcastEventHandler<DownloadProgressUpdatedEvent>>();

        return services;
    }
}