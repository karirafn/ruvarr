using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

namespace Ruvarr.Api.Programs.Queries;

internal static class WatchDownloadQueueEndpoint
{
    internal static RouteGroupBuilder MapWatchDownloadQueueEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/download-queue/stream", (
            [FromServices] DownloadQueueNotifier notifier,
            [FromServices] IServiceScopeFactory scopeFactory,
            CancellationToken cancellationToken) =>
        {
            return TypedResults.ServerSentEvents(Stream(notifier, scopeFactory, cancellationToken));
        })
        .WithSummary("Streams download queue updates.")
        .Produces(StatusCodes.Status200OK);

        return group;
    }

    private static async IAsyncEnumerable<List<DownloadQueueItemSummary>> Stream(
        DownloadQueueNotifier notifier,
        IServiceScopeFactory scopeFactory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return await QueryAsync(scopeFactory, cancellationToken);

        await foreach (byte _ in notifier.WatchAsync(cancellationToken))
        {
            yield return await QueryAsync(scopeFactory, cancellationToken);
        }
    }

    private static async Task<List<DownloadQueueItemSummary>> QueryAsync(IServiceScopeFactory scopeFactory, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>>();
        return await handler.Handle(new GetDownloadQueueQuery(IncludeDownloaded: true), cancellationToken);
    }
}
