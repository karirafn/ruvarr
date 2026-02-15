using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ruvarr.Sonarr;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSonarr(this IServiceCollection services)
    {
        services.AddOptions<SonarrOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(SonarrOptions.SectionName).Bind(options));

        IOptions<SonarrOptions> options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<SonarrOptions>>();

        services.AddTransient<ISonarrClient, SonarrClient>();
        services.AddHttpClient<ISonarrClient, SonarrClient>(client =>
        {
            client.BaseAddress = options.Value.BaseAddress;
            client.DefaultRequestHeaders.Add("X-Api-Key", options.Value.ApiKey);
        });

        return services;
    }
}