using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramMatchedTvdbEvent(int RuvId, string Name) : IDomainEvent;
