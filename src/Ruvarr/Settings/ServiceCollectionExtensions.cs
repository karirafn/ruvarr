using Ruvarr.Abstractions;
using Ruvarr.Settings.Commands.SaveSettings;
using Ruvarr.Settings.Queries.GetSettings;

namespace Ruvarr.Settings;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSettings(this IServiceCollection services, string settingsFilePath)
    {
        services.AddSingleton<SettingsStore>(_ => new SettingsStore(settingsFilePath));
        services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<SettingsStore>());
        services.AddTransient<IRequestHandler<SaveSettingsCommand>, SaveSettingsHandler>();
        services.AddTransient<IRequestHandler<GetSettingsQuery, Result<RuvarrSettings>>, GetSettingsHandler>();

        return services;
    }
}
