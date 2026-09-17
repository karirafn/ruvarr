using Microsoft.Extensions.Options;

using Ruvarr.Abstractions;

namespace Ruvarr.Infrastructure.Ruv;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRuv(this IServiceCollection services)
    {
        services.AddOptions<RuvOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(RuvOptions.SectionName).Bind(options));

        services.AddTransient<IRuvClient, RuvClient>();
        services.AddHttpClient<IRuvClient, RuvClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IOptions<RuvOptions>>().Value.BaseAddress;
            client.Timeout = ApiClientTimeouts.Default;
        });
        services.AddHttpClient<IRuvStreamInspector, RuvStreamInspector>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}