using Ruvarr.Abstractions;

namespace Ruvarr.TvdbEpisodeLookup;

internal sealed class TvdbEpisodeMatcher(
    IEnumerable<IEpisodeMatchingStrategy> strategies,
    ILogger<TvdbEpisodeMatcher> logger) : ITvdbEpisodeMatcher
{
    public async Task MatchAsync(EpisodeMatchingContext context, CancellationToken cancellationToken)
    {
        foreach (IEpisodeMatchingStrategy strategy in strategies)
        {
            logger.LogDebug("Running {Strategy}", strategy.GetType().Name);
            await strategy.MatchAsync(context, cancellationToken);
        }
    }
}
