using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Commands.AddProgramToSonarr;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Commands.MatchProgram;
using Ruvarr.Programs.Commands.MatchProgramEpisodes;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Events;
using Ruvarr.Programs.Queries.GetProgram;
using Ruvarr.Programs.Queries.GetProgramEpisodes;
using Ruvarr.Programs.Queries.GetPrograms;
using Ruvarr.Programs.Queries.GetSonarrQualityProfiles;
using Ruvarr.Programs.Queries.GetSonarrRootFolders;
using Ruvarr.Programs.Queries.GetTvdbSeriesEpisodes;
using Ruvarr.Programs.Queries.SearchTvdbSeries;

namespace Ruvarr.Programs;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddPrograms(this IServiceCollection services)
    {
        services.AddTransient<IDomainEventHandler<ProgramCreatedEvent>, ProgramCreatedEventHandler>();
        services.AddTransient<IDomainEventHandler<ProgramMatchedTvdbEvent>, ProgramMatchedTvdbEventHandler>();
        services.AddTransient<IDomainEventHandler<ProgramMatchedTmdbEvent>, ProgramMatchedTmdbEventHandler>();
        services.AddTransient<IDomainEventHandler<ProgramRefreshRequestedEvent>, ProgramRefreshRequestedEventHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeAddedToMatchedProgramEvent>, EpisodeAddedToMatchedProgramEventHandler>();
        services.AddTransient<IDomainEventHandler<EpisodeMatchedEvent>, EpisodeMatchedEventHandler>();
        services.AddTransient<IRequestHandler<GetProgramQuery, ProgramSummary?>, GetProgramHandler>();
        services.AddTransient<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>, GetProgramsHandler>();
        services.AddTransient<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>, GetProgramEpisodesHandler>();
        services.AddTransient<IRequestHandler<GetTvdbSeriesEpisodesQuery, IReadOnlyList<TvdbSeriesEpisode>>, GetTvdbSeriesEpisodesHandler>();
        services.AddTransient<IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>, SearchTvdbSeriesHandler>();
        services.AddTransient<IRequestHandler<MatchEpisodeCommand>, MatchEpisodeHandler>();
        services.AddTransient<IRequestHandler<MatchProgramCommand>, MatchProgramHandler>();
        services.AddTransient<IRequestHandler<DownloadEpisodeCommand>, DownloadEpisodeHandler>();
        services.AddTransient<IRequestHandler<MatchProgramEpisodesCommand>, MatchProgramEpisodesHandler>();
        services.AddTransient<IRequestHandler<RefreshProgramCommand>, RefreshProgramHandler>();
        services.AddTransient<IRequestHandler<AddProgramToSonarrCommand>, AddProgramToSonarrHandler>();
        services.AddTransient<IRequestHandler<GetSonarrRootFoldersQuery, IReadOnlyList<RootFolder>>, GetSonarrRootFoldersHandler>();
        services.AddTransient<IRequestHandler<GetSonarrQualityProfilesQuery, IReadOnlyList<QualityProfile>>, GetSonarrQualityProfilesHandler>();
        services.AddScoped<ProgramsFilterState>();

        return services;
    }
}