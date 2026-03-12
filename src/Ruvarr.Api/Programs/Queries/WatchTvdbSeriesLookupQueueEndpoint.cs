using Microsoft.AspNetCore.Mvc;

using Ruvarr.Contracts;
using Ruvarr.Programs;

namespace Ruvarr.Api.Programs.Queries;

internal static class WatchTvdbSeriesLookupQueueEndpoint
{
    internal static RouteGroupBuilder MapWatchTvdbSeriesLookupQueueEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/tvdb-series-lookup-queue/stream", (
            [FromServices] TvdbSeriesLookupNotifier lookupQueue,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.ServerSentEvents(Stream(lookupQueue, cancellationToken));
        })
        .WithSummary("Streams TVDB series lookup queue updates.")
        .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async IAsyncEnumerable<IReadOnlyList<TvdbSeriesLookupQueueItemSummary>> Stream(
        TvdbSeriesLookupNotifier lookupQueue,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return lookupQueue.Items;

        await foreach (byte _ in lookupQueue.WatchAsync(cancellationToken))
        {
            yield return lookupQueue.Items;
        }
    }
}
