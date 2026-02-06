using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ruvarr.Tvdb;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddTvdb(this IServiceCollection services)
    {
        services.AddOptions<TvdbOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(TvdbOptions.SectionName).Bind(options));

        return services;
    }
}
