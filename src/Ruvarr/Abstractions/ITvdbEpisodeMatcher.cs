using Ruvarr.TvdbEpisodeLookup;

namespace Ruvarr.Abstractions;

internal interface ITvdbEpisodeMatcher
{
    Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken);
}
