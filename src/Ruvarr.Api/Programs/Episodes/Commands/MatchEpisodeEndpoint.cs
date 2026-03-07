using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Commands.MatchEpisode;
using Ruvarr.Tvdb;

namespace Ruvarr.Api.Programs.Episodes.Commands;

internal static class MatchEpisodeEndpoint
{
    internal const string Name = "MatchEpisode";

    internal static RouteGroupBuilder MapMatchEpisodeEndpoint(this RouteGroupBuilder group)
    {

        group.MapPost("/episodes/{ruvId}/match/{tvdbId:int}", static async (
            [FromRoute] string ruvId,
            [FromRoute] int tvdbId,
            [FromServices] MatchEpisodeHandler handler,
            CancellationToken cancellationToken) =>
        {
            RuvarrResult result = await handler.Handle(new MatchEpisodeCommand(ruvId, tvdbId), cancellationToken);
            return result.Match<IResult>(
                success: TypedResults.NoContent,
                failure: error => error.Code switch
                {
                    RuvErrors.EpisodeNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    TvdbErrors.EpisodeNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
                    _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, detail: error.Description)
                });
        })
        .WithName(Name)
        .WithSummary("Creates a new user.")
        .WithDescription("Adds a new user to the database and returns its Id.")
        .Produces<int>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}