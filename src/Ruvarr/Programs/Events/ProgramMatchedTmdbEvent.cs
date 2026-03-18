using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramMatchedTmdbEvent(int RuvId, string Name) : IDomainEvent;
