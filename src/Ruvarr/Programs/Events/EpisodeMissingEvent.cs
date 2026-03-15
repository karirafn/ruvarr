using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Events;

internal sealed record EpisodeMissingEvent(RuvEpisode Episode) : IDomainEvent;