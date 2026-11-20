using Ruvarr.Abstractions;
using Ruvarr.Downloads.Events;
using Ruvarr.Settings;

namespace Ruvarr.Downloads;

internal sealed class DeleteDismissedDownloadFilesHandler(
    DownloadFileStore fileStore,
    ISettingsStore settingsStore)
    : IDomainEventHandler<DownloadDismissedEvent>
{
    public Task Handle(DownloadDismissedEvent @event, CancellationToken cancellationToken)
    {
        if (@event.Item.FileName is not { } fileName)
        {
            return Task.CompletedTask;
        }

        RuvarrSettings settings = settingsStore.Current;
        fileStore.DeleteCompleted(settings, fileName);
        fileStore.DeleteIncomplete(settings, fileName);

        return Task.CompletedTask;
    }
}
