using Ruvarr.Abstractions;
using Ruvarr.Downloads.Notifiers;

namespace Ruvarr.Downloads.Events;

internal sealed class DownloadCompletedEventHandler(DownloadQueueNotifier notifier) : IDomainEventHandler<DownloadCompletedEvent>
{
    public Task Handle(DownloadCompletedEvent @event, CancellationToken cancellationToken)
    {
        notifier.Notify();
        return Task.CompletedTask;
    }
}
