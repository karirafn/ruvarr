using Microsoft.Extensions.Caching.Memory;

namespace Ruvarr.Infrastructure.Sonarr;

internal static class ServiceCollectionExtensions
{
    // Never dialled: SonarrDelegatingHandler rewrites every request against the configured
    // base address. It exists only because HttpClient requires an absolute BaseAddress.
    private static readonly Uri PlaceholderBaseAddress = new("https://unconfigured");

    internal static IServiceCollection AddSonarr(this IServiceCollection services)
    {
        services.AddTransient<SonarrDelegatingHandler>();
        services.AddHttpClient<SonarrClient>(client =>
            {
                client.BaseAddress = PlaceholderBaseAddress;
            })
            .AddHttpMessageHandler<SonarrDelegatingHandler>();

        services.AddSingleton<ISonarrClient>(sp =>
            new CachingSonarrClient(
                () => sp.GetRequiredService<SonarrClient>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingSonarrClient>>()));

        return services;
    }
}
