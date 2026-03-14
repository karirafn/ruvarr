using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Queries.GetProgramEpisodes;

namespace Ruvarr.Api.Programs.Queries;

internal static class GetProgramEpisodesEndpoint
{
    internal static RouteGroupBuilder MapGetProgramEpisodesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{ruvId}/episodes", static async (
            [FromServices] IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> handler,
            [FromRoute] int ruvId,
            CancellationToken cancellationToken) =>
        {
            GetProgramEpisodesQuery query = new(ProgramRuvId: ruvId);
            List<EpisodeSummary> result = await handler.Handle(query, cancellationToken);
            return TypedResults.Ok(result);
        })
        .WithSummary("Gets episodes for a program.")
        .WithDescription("Gets episodes for a program by its RÚV ID.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
