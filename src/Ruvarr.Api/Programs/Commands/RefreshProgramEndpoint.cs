using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;

namespace Ruvarr.Api.Programs.Commands;

internal static class RefreshProgramEndpoint
{
    internal const string Name = "RefreshProgram";

    internal static RouteGroupBuilder MapRefreshProgramEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{ruvId}/refresh", static async (
            [FromRoute] int ruvId,
            [FromServices] IRequestHandler<RefreshProgramCommand> handler,
            CancellationToken cancellationToken) =>
        {
            RuvarrResult result = await handler.Handle(new RefreshProgramCommand(ruvId), cancellationToken);
            return result.Match<IResult>(
                success: TypedResults.NoContent,
                failure: error => error.Code switch
                {
                    ProgramErrors.ProgramNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Description)
                });
        })
        .WithName(Name)
        .WithSummary("Refreshes a RÚV program.")
        .WithDescription("Enqueues a RÚV program for refresh.")
        .Produces<int>(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
