using Ruvarr.Programs.Commands;
using Ruvarr.Programs.Queries;

namespace Ruvarr.Programs;

internal static class ProgramEndpoints
{
    internal static IEndpointRouteBuilder MapPogramEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/programs")
            .WithTags("Programs");

        group.MapMatchEpisodeEndpoint();
        group.MapMatchProgramEpisodesEndpoint();
        group.MapDownloadEpisodeEndpoint();
        group.MapRefreshProgramEndpoint();
        group.MapGetProgramListEndpoint();
        group.MapGetProgramEndpoint();
        group.MapGetProgramEpisodesEndpoint();
        group.MapGetProgramsEndpoint();
        group.MapGetDownloadQueueEndpoint();
        group.MapWatchDownloadQueueEndpoint();
        group.MapWatchProgramRefreshNotifierEndpoint();
        group.MapWatchTvdbSeriesLookupQueueEndpoint();
        group.MapWatchTvdbEpisodeLookupQueueEndpoint();

        return group;
    }
}