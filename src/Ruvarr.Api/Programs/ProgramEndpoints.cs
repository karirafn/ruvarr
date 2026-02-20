using Ruvarr.Api.Programs.Episodes;

namespace Ruvarr.Api.Programs;

internal static class ProgramEndpoints
{
    internal static IEndpointRouteBuilder MapPogramEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/programs")
            .WithTags("Programs");

        group.MapDownloadEpisodeEndpoint();

        return group;
    }
}
