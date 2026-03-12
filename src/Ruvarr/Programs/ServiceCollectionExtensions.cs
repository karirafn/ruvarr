using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Commands.MatchProgramEpisodes;
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

        return services;
    }
}