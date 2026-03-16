using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramMonitoredStatusChangedEvent(int RuvId, bool IsMonitored) : IDomainEvent;
