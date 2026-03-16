using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Events;

internal sealed record EpisodeLookupScheduledEvent(RuvEpisode Episode) : IDomainEvent;
