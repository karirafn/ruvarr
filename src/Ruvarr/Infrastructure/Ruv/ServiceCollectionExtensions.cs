using Microsoft.Extensions.Options;

namespace Ruvarr.Infrastructure.Ruv;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRuv(this IServiceCollection services)
    {
        services.AddOptions<RuvOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(RuvOptions.SectionName).Bind(options));

        IOptions<RuvOptions> options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<RuvOptions>>();

        services.AddTransient<IRuvClient, RuvClient>();
        services.AddHttpClient<IRuvClient, RuvClient>(client => client.BaseAddress = options.Value.BaseAddress);

        return services;
    }
}