using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record EpisodeAddedToMatchedProgramEvent(int RuvId, string Name) : IDomainEvent;
