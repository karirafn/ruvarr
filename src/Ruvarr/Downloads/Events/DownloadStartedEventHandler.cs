using Ruvarr.Abstractions;
using Ruvarr.Downloads.Notifiers;

namespace Ruvarr.Downloads.Events;

internal sealed class DownloadStartedEventHandler(DownloadQueueNotifier notifier) : IDomainEventHandler<DownloadStartedEvent>
{
    public Task Handle(DownloadStartedEvent @event, CancellationToken cancellationToken)
    {
        notifier.Notify();
        return Task.CompletedTask;
    }
}
