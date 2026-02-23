using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Commands.DownloadEpisode;

namespace Ruvarr.Api.Programs.Episodes.Commands;

internal static class DownloadEpisodeEndpoint
{
    internal const string Name = "DownloadEpisode";

    internal static RouteGroupBuilder MapDownloadEpisodeEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/episodes/{ruvId}/download", static async (
            [FromRoute] string ruvId,
            [FromServices] DownloadEpisodeHandler handler,
            CancellationToken cancellationToken) =>
        {
            RuvarrResult result = await handler.Handle(new DownloadEpisodeCommand(ruvId), cancellationToken);
            return result.Match<IResult>(
                success: TypedResults.NoContent,
                failure: error => error.Code switch
                {
                    RuvErrors.EpisodeNotFoundCode => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, detail: error.Description),
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