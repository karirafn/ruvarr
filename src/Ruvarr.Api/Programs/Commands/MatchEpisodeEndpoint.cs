using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Tvdb;
using Ruvarr.Programs;

namespace Ruvarr.Api.Programs.Commands;

internal static class MatchEpisodeEndpoint
{
    internal const string Name = "MatchEpisode";

    internal static RouteGroupBuilder MapMatchEpisodeEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/episodes/{ruvId}/match/{tvdbId:int}", static async (
            [FromRoute] string ruvId,
            [FromRoute] int tvdbId,
            [FromServices] IRequestHandler<MatchEpisodeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            RuvarrResult result = await handler.Handle(new MatchEpisodeCommand(ruvId, tvdbId), cancellationToken);
            return result.Match<IResult>(
                success: TypedResults.NoContent,
                failure: error => error.Code switch
                {
                    ProgramErrors.EpisodeNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    TvdbErrors.EpisodeNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Description)
                });
        })
        .WithName(Name)
        .WithSummary("Matches a RÚV episode with a TVDB episode.")
        .WithDescription("Matches a RÚV episode id with a TVDB episode id.")
        .Produces<int>(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
