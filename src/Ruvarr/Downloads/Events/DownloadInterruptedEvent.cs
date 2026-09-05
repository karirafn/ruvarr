using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;

namespace Ruvarr.Downloads.Events;

internal sealed record DownloadInterruptedEvent(DownloadQueueItem Item) : IDomainEvent;
