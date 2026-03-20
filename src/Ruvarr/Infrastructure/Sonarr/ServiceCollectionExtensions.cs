namespace Ruvarr.Infrastructure.Sonarr;

internal static class ServiceCollectionExtensions
{
    private static readonly Uri PlaceholderBaseAddress = new("http://unconfigured");

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
                sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingSonarrClient>>()));

        return services;
    }
}
