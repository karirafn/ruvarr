using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Commands.MatchProgramEpisodes;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Queries.GetEpisodes;

namespace Ruvarr.Programs;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddPrograms(this IServiceCollection services)
    {
        services.AddTransient<IRequestHandler<GetEpisodesQuery, List<ProgramSummary>>, GetEpisodesHandler>();
        services.AddTransient<IRequestHandler<MatchEpisodeCommand>, MatchEpisodeHandler>();
        services.AddTransient<IRequestHandler<DownloadEpisodeCommand>, DownloadEpisodeHandler>();
        services.AddTransient<IRequestHandler<MatchProgramEpisodesCommand>, MatchProgramEpisodesHandler>();
        services.AddTransient<IRequestHandler<RefreshProgramCommand>, RefreshProgramHandler>();

        return services;
    }
}