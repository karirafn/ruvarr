using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Commands.MatchProgramEpisodes;
using Ruvarr.Tvdb;

namespace Ruvarr.Api.Programs.Commands;

internal static class MatchProgramEpisodesEndpoint
{
    internal const string Name = "MatchProgramEpisodes";

    internal static RouteGroupBuilder MapMatchProgramEpisodesEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{ruvId:int}/episodes/match", static async (
            [FromRoute] int ruvId,
            [FromServices] IRequestHandler<MatchProgramEpisodesCommand> handler,
            CancellationToken cancellationToken) =>
        {
            RuvarrResult result = await handler.Handle(new MatchProgramEpisodesCommand(ruvId), cancellationToken);
            return result.Match<IResult>(
                success: TypedResults.NoContent,
                failure: error => error.Code switch
                {
                    RuvErrors.ProgramNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    TvdbErrors.SeriesNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Description)
                });
        })
        .WithName(Name)
        .WithSummary("Tries to match all episodes of a RÚV program.")
        .WithDescription("Tries to match all episodes of a RÚV program by parsing its name if it's on the forma 'Þáttur x' where 'x' is an integer to a TVDB episode. Uses the name of the program if its title ends with a roman numeral as the season number.")
        .Produces<int>(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
