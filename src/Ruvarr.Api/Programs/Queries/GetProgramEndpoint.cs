using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Queries.GetProgram;

namespace Ruvarr.Api.Programs.Queries;

internal static class GetProgramEndpoint
{
    internal static RouteGroupBuilder MapGetProgramEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{ruvId:int}", static async (
            [FromServices] IRequestHandler<GetProgramQuery, ProgramSummary?> handler,
            [FromRoute] int ruvId,
            CancellationToken cancellationToken) =>
        {
            GetProgramQuery query = new(ProgramRuvId: ruvId);
            ProgramSummary? result = await handler.Handle(query, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        })
        .WithSummary("Gets a single program summary.")
        .WithDescription("Gets a program summary by its RÚV ID.")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
