using Microsoft.AspNetCore.Mvc;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Queries.GetPrograms;

namespace Ruvarr.Programs.Queries;

internal static class GetProgramListEndpoint
{
    internal static RouteGroupBuilder MapGetProgramListEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("", static (
            [FromServices] IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> handler,
            [FromQuery] string? channel,
            [FromQuery] bool? isProgramMonitored,
            [FromQuery] bool? isProgramMissingEpisodes,
            [FromQuery] bool? isProgramMatched,
            [FromQuery] bool? isProgramPartiallyMatched,
            CancellationToken cancellationToken) =>
        {
            GetProgramsQuery query = new(
                Channel: channel,
                IsProgramMonitored: isProgramMonitored,
                IsProgramMissingEpisodes: isProgramMissingEpisodes,
                IsProgramMatched: isProgramMatched,
                IsProgramPartiallyMatched: isProgramPartiallyMatched);

            return handler.Handle(query, cancellationToken);
        })
        .WithSummary("Gets program summaries.")
        .WithDescription("Gets program summaries without episodes.")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }
}