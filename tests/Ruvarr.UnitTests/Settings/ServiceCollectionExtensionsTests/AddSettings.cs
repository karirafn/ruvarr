using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.ServiceCollectionExtensionsTests;

public sealed class AddSettings : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public AddSettings()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void TakesDownloadsRootFromConfiguration()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider(new Dictionary<string, string?> { ["Ruvarr:DownloadsRoot"] = "/data/torrents" });

        // Act
        RuvarrSettings settings = provider.GetRequiredService<ISettingsStore>().Current;

        // Assert
        settings.DownloadsRoot.ShouldBe("/data/torrents");
    }

    [Fact]
    public void DefaultsDownloadsRootWhenNotConfigured()
    {
        // Arrange
        using ServiceProvider provider = BuildProvider([]);

        // Act
        RuvarrSettings settings = provider.GetRequiredService<ISettingsStore>().Current;

        // Assert
        settings.DownloadsRoot.ShouldBe("/downloads");
    }

    private ServiceProvider BuildProvider(Dictionary<string, string?> configurationValues)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddSettings(Path.Combine(_tempDirectory, "settings.json"));
        return services.BuildServiceProvider();
    }
}
