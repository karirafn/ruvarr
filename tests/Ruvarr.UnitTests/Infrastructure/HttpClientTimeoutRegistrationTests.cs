// Unit test route: calls the three ServiceCollectionExtensions.Add* methods on a fresh
// ServiceCollection so the test verifies DI wiring without requiring Docker or a real database.
// The integration host cannot be trivially used here because the full AddRuvarr registration
// requires a SQLite connection string, settings file, and Quartz wiring that are out of scope
// for a registration timeout assertion.

using NSubstitute;

using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure;

public sealed class HttpClientTimeoutRegistrationTests
{
    private static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public void WhenRuvClientRegistered_HttpClientTimeoutIsInfiniteSoPipelineOwnsTimeout()
    {
        // Arrange — the resilience pipeline owns the timeout budget; HttpClient.Timeout is
        // Infinite so the pipeline is the sole authority (see ADR 0010).
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ruv:BaseAddress"] = "https://api.ruv.is/",
        });

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        using HttpClient client = factory.CreateClient(nameof(IRuvClient));

        // Assert
        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void WhenSonarrClientRegistered_HttpClientTimeoutIsInfiniteSoPipelineOwnsTimeout()
    {
        // Arrange — the resilience pipeline owns the timeout budget; HttpClient.Timeout is
        // Infinite so the pipeline is the sole authority (see ADR 0010).
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings());

        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddSingleton(settingsStore);
        services.AddSonarr();

        ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        using HttpClient client = factory.CreateClient(nameof(SonarrClient));

        // Assert
        client.Timeout.ShouldBe(Timeout.InfiniteTimeSpan);
    }

    [Fact]
    public void WhenTvdbClientRegistered_HttpClientTimeoutIs30Seconds()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Tvdb:BaseAddress"] = "https://api4.thetvdb.com/",
        });

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings());

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddMemoryCache();
        services.AddSingleton(settingsStore);
        services.AddTvdb();

        ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        using HttpClient client = factory.CreateClient(nameof(ITvdbClient));

        // Assert
        client.Timeout.ShouldBe(ExpectedTimeout);
    }

    [Fact]
    public void WhenRuvStreamInspectorRegistered_HttpClientTimeoutRemains10Seconds()
    {
        // Arrange — RuvStreamInspector has an explicit 10s timeout that must not be changed.
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Ruv:BaseAddress"] = "https://api.ruv.is/",
        });

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();

        // Act
        using HttpClient client = factory.CreateClient(nameof(IRuvStreamInspector));

        // Assert
        client.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}
