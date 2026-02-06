using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Ruvarr.Internals.Clients;
using Ruvarr.Internals.Options;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services)
    {
        services.AddOptions<RuvarrOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(RuvarrOptions.SectionName).Bind(options));

        IOptions<RuvarrOptions> options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<RuvarrOptions>>();

        services.AddTransient<IRuvClient, RuvClient>();
        services.AddHttpClient<IRuvClient, RuvClient>(client => client.BaseAddress = options.Value.Ruv.ApiBaseAddress);

        return services;
    }
}