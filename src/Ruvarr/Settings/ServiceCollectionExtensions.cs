using Ruvarr.Abstractions;
using Ruvarr.Settings.Commands.SaveSettings;
using Ruvarr.Settings.Queries.GetDistinctChannels;
using Ruvarr.Settings.Queries.GetSettings;
using Ruvarr.Settings.Queries.TestSonarrConnection;

namespace Ruvarr.Settings;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSettings(this IServiceCollection services, string settingsFilePath)
    {
        services.AddSingleton<SettingsStore>(sp => new SettingsStore(
            settingsFilePath,
            sp.GetRequiredService<IConfiguration>()["Ruvarr:DownloadsRoot"] ?? RuvarrSettings.DefaultDownloadsRoot));
        services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<SettingsStore>());
        services.AddTransient<IRequestHandler<SaveSettingsCommand>, SaveSettingsHandler>();
        services.AddTransient<IRequestHandler<GetSettingsQuery, Result<RuvarrSettings>>, GetSettingsHandler>();
        services.AddTransient<IRequestHandler<TestSonarrConnectionQuery>, TestSonarrConnectionHandler>();
        services.AddTransient<IRequestHandler<GetDistinctChannelsQuery, List<string>>, GetDistinctChannelsHandler>();

        return services;
    }
}
