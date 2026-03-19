namespace Ruvarr.Infrastructure.Sonarr;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSonarr(this IServiceCollection services)
    {
        services.AddTransient<SonarrDelegatingHandler>();
        services.AddHttpClient<ISonarrClient, SonarrClient>()
            .AddHttpMessageHandler<SonarrDelegatingHandler>();

        return services;
    }
}
