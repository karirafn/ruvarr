using Ruvarr.Abstractions;

namespace Ruvarr.Programs.Events;

internal sealed record ProgramMissingEpisodesChangedEvent(int RuvId, bool HasMissingEpisodes) : IDomainEvent;
