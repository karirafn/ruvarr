using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Commands.MatchProgram;
using Ruvarr.Programs.Commands.MatchProgramEpisodes;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Events;
using Ruvarr.Programs.Notifiers;
using Ruvarr.Programs.Queries.GetProgram;
using Ruvarr.Programs.Queries.GetProgramEpisodes;
using Ruvarr.Programs.Queries.GetPrograms;

namespace Ruvarr.Programs;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddPrograms(this IServiceCollection services)
    {
        services.AddSingleton<ProgramCreatedNotifier>();
        services.AddTransient<IDomainEventHandler<ProgramCreatedEvent>, ProgramCreatedEventHandler>();
        services.AddTransient<IDomainEventHandler<ProgramMatchedTvdbEvent>, ProgramMatchedTvdbEventHandler>();
        services.AddTransient<IRequestHandler<GetProgramQuery, ProgramSummary?>, GetProgramHandler>();
        services.AddTransient<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>, GetProgramsHandler>();
        services.AddTransient<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>, GetProgramEpisodesHandler>();
        services.AddTransient<IRequestHandler<MatchEpisodeCommand>, MatchEpisodeHandler>();
        services.AddTransient<IRequestHandler<MatchProgramCommand>, MatchProgramHandler>();
        services.AddTransient<IRequestHandler<DownloadEpisodeCommand>, DownloadEpisodeHandler>();
        services.AddTransient<IRequestHandler<MatchProgramEpisodesCommand>, MatchProgramEpisodesHandler>();
        services.AddTransient<IRequestHandler<RefreshProgramCommand>, RefreshProgramHandler>();

        return services;
    }
}