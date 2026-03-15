using Microsoft.AspNetCore.Mvc;

using Ruvarr.Contracts;

namespace Ruvarr.Programs.Queries;

internal static class WatchProgramRefreshNotifierEndpoint
{
    internal static RouteGroupBuilder MapWatchProgramRefreshNotifierEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/program-refresh-queue/stream", (
            [FromServices] ProgramRefreshNotifier refreshQueue,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.ServerSentEvents(Stream(refreshQueue, cancellationToken));
        })
        .WithSummary("Streams program refresh queue updates.")
        .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async IAsyncEnumerable<IReadOnlyList<ProgramRefreshQueueItemSummary>> Stream(
        ProgramRefreshNotifier refreshQueue,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return refreshQueue.Items;

        await foreach (byte _ in refreshQueue.WatchAsync(cancellationToken))
        {
            yield return refreshQueue.Items;
        }
    }
}