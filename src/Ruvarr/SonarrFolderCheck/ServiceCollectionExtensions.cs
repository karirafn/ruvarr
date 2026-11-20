namespace Ruvarr.SonarrFolderCheck;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSonarrFolderCheck(this IServiceCollection services)
    {
        services.AddSingleton<SonarrFolderCheckHealth>();
        services.AddTransient<SonarrFolderChecker>();
        services.AddTransient<SonarrFolderCheckRequeuer>();

        return services;
    }
}
