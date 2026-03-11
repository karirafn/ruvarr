using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

namespace Ruvarr.Api.Programs.Queries;

internal static class GetDownloadQueueEndpoint
{
    internal const string Name = "GetDownloadQueue";

    internal static RouteGroupBuilder MapGetDownloadQueueEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/download-queue", static async (
            [FromServices] IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler,
            [FromQuery] bool includeDownloaded = false,
            CancellationToken cancellationToken = default) =>
        {
            List<DownloadQueueItemSummary> result = await handler.Handle(new GetDownloadQueueQuery(includeDownloaded), cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithName(Name)
        .WithSummary("Gets the download queue.")
        .WithDescription("Gets all items in the download queue.")
        .Produces<List<DownloadQueueItemSummary>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
