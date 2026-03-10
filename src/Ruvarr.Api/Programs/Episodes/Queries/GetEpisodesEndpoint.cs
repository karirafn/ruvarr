using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Ruv.Queries.GetEpisodes;

namespace Ruvarr.Api.Programs.Episodes.Queries;

internal static class GetEpisodesEndpoint
{
    internal const string Name = "GetEpisodes";

    internal static RouteGroupBuilder MapGetEpisodesEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/episodes", static async (
            [FromServices] IRequestHandler<GetEpisodesQuery, List<EpisodeSummary>> handler,
            [FromQuery] string? channel,
            [FromQuery] string? programName,
            [FromQuery] bool? isProgramMonitored,
            [FromQuery] bool? isProgramMissingEpisodes,
            [FromQuery] bool? isProgramMatched,
            [FromQuery] bool? isEpisodeMatched,
            [FromQuery] bool? isEpisodeMissing,
            CancellationToken cancellationToken) =>
        {
            GetEpisodesQuery query = new(
                Channel: channel,
                ProgramName: programName,
                IsProgramMonitored: isProgramMonitored,
                IsProgramMissingEpisodes: isProgramMissingEpisodes,
                IsProgramMatched: isProgramMatched,
                IsEpisodeMatched: isEpisodeMatched,
                IsEpisodeMissing: isEpisodeMissing);

            List<EpisodeSummary> result = await handler.Handle(query, cancellationToken);

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