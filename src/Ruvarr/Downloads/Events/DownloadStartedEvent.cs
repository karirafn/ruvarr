using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Events;

internal sealed record DownloadStartedEvent(DownloadQueueItem Item) : IDomainEvent;
