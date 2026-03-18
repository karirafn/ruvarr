using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramRefreshRequestedEvent(int RuvId, string Name, bool HasSeries, bool HasMultipleEpisodes) : IDomainEvent;
