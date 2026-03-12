using Microsoft.AspNetCore.Mvc;

using Ruvarr.Contracts;
using Ruvarr.Programs;

namespace Ruvarr.Api.Programs.Queries;

internal static class WatchTvdbEpisodeLookupQueueEndpoint
{
    internal static RouteGroupBuilder MapWatchTvdbEpisodeLookupQueueEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/tvdb-episode-lookup-queue/stream", (
            [FromServices] TvdbEpisodeLookupNotifier lookupQueue,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.ServerSentEvents(Stream(lookupQueue, cancellationToken));
        })
        .WithSummary("Streams TVDB episode lookup queue updates.")
        .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async IAsyncEnumerable<IReadOnlyList<TvdbEpisodeLookupQueueItemSummary>> Stream(
        TvdbEpisodeLookupNotifier lookupQueue,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return lookupQueue.Items;

        await foreach (byte _ in lookupQueue.WatchAsync(cancellationToken))
        {
            yield return lookupQueue.Items;
        }
    }
}
