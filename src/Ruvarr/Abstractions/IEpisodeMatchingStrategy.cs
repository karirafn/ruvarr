using Ruvarr.TvdbEpisodeLookup;

namespace Ruvarr.Abstractions;

internal interface IEpisodeMatchingStrategy
{
    Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken);
}
