using Ruvarr.Api.Programs.Episodes.Commands;
using Ruvarr.Api.Programs.Episodes.Queries;

namespace Ruvarr.Api.Programs;

internal static class ProgramEndpoints
{
    internal static IEndpointRouteBuilder MapPogramEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/programs")
            .WithTags("Programs");

        group.MapMatchEpisodeEndpoint();
        group.MapMatchProgramEpisodesEndpoint();
        group.MapDownloadEpisodeEndpoint();
        group.MapGetEpisodesEndpoint();

        return group;
    }
}
