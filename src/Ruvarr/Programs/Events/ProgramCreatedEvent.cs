using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramCreatedEvent(RuvProgram Program) : IDomainEvent;
