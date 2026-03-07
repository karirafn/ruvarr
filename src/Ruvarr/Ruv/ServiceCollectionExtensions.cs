using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Ruvarr.Abstractions;
using Ruvarr.Ruv.Commands.DownloadEpisode;
using Ruvarr.Ruv.Commands.MatchEpisode;
using Ruvarr.Ruv.Queries.GetEpisodes;

namespace Ruvarr.Ruv;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRuv(this IServiceCollection services)
    {
        services.AddOptions<RuvOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(RuvOptions.SectionName).Bind(options));

        IOptions<RuvOptions> options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<RuvOptions>>();

        services.AddTransient<MatchEpisodeHandler>();
        services.AddTransient<IRequestHandler<DownloadEpisodeCommand>, DownloadEpisodeHandler>();
        services.AddTransient<IRequestHandler<GetEpisodesQuery, List<EpisodeSummary>>, GetEpisodesHandler>();

        services.AddTransient<IRuvClient, RuvClient>();
        services.AddHttpClient<IRuvClient, RuvClient>(client => client.BaseAddress = options.Value.BaseAddress);

        return services;
    }
}