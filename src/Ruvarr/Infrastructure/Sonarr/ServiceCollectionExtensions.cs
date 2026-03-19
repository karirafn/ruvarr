namespace Ruvarr.Infrastructure.Sonarr;

internal static class ServiceCollectionExtensions
{
    private static readonly Uri PlaceholderBaseAddress = new("http://unconfigured");

    internal static IServiceCollection AddSonarr(this IServiceCollection services)
    {
        services.AddTransient<SonarrDelegatingHandler>();
        services.AddHttpClient<ISonarrClient, SonarrClient>(client =>
            {
                client.BaseAddress = PlaceholderBaseAddress;
            })
            .AddHttpMessageHandler<SonarrDelegatingHandler>();

        return services;
    }
}
