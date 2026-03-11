using Ruvarr.Api.Programs.Commands;
using Ruvarr.Api.Programs.Queries;

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
        group.MapGetProgramsEndpoint();
        group.MapGetDownloadQueueEndpoint();
        group.MapWatchDownloadQueueEndpoint();

        return group;
    }
}
