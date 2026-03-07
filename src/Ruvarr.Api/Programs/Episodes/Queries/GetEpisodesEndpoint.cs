using Microsoft.AspNetCore.Mvc;

using Ruvarr.Ruv.Queries.GetEpisodes;

namespace Ruvarr.Api.Programs.Episodes.Queries;

internal static class GetEpisodesEndpoint
{
    internal const string Name = "GetEpisodes";

    internal static RouteGroupBuilder MapGetEpisodesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/episodes", static async (
            [FromServices] GetEpisodesHandler handler,
            [FromQuery] string? programName,
            [FromQuery] bool? isProgramMatched,
            [FromQuery] bool? isEpisodeMatched,
            CancellationToken cancellationToken) =>
        {
            List<EpisodeSummary> result = await handler.Handle(
                programName: programName,
                isEpisodeMatched: isEpisodeMatched,
                isProgramMatched: isProgramMatched,
                cancellationToken: cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName(Name)
        .WithSummary("Gets episodes.")
        .WithDescription("Gets episodes.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}