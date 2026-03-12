using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ruvarr.Infrastructure.Tvdb;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddTvdb(this IServiceCollection services)
    {
        services.AddOptions<TvdbOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(TvdbOptions.SectionName).Bind(options));

        IOptions<TvdbOptions> options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<TvdbOptions>>();

        services.AddTransient<ITvdbClient, TvdbClient>();
        services.AddHttpClient<ITvdbClient, TvdbClient>(client => client.BaseAddress = options.Value.BaseAddress);

        return services;
    }
}
