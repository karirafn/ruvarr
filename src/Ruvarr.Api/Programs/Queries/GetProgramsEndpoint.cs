using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Queries.GetEpisodes;

namespace Ruvarr.Api.Programs.Queries;

internal static class GetProgramsEndpoint
{
    internal const string Name = "GetPrograms";

    internal static RouteGroupBuilder MapGetProgramsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/episodes", static async (
            [FromServices] IRequestHandler<GetEpisodesQuery, List<ProgramDetails>> handler,
            [FromQuery] string? channel,
            [FromQuery] string? programName,
            [FromQuery] bool? isProgramMonitored,
            [FromQuery] bool? isProgramMissingEpisodes,
            [FromQuery] bool? isProgramMatched,
            [FromQuery] bool? isProgramPartiallyMatched,
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
                IsProgramPartiallyMatched: isProgramPartiallyMatched,
                IsEpisodeMatched: isEpisodeMatched,
                IsEpisodeMissing: isEpisodeMissing);

            List<ProgramDetails> result = await handler.Handle(query, cancellationToken);

            return TypedResults.Ok(result);
        })
        .WithName(Name)
        .WithSummary("Gets programs.")
        .WithDescription("Gets programs.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}
