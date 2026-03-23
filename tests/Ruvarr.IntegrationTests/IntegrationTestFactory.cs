
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Ruvarr.Settings;

namespace Ruvarr.IntegrationTests;

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-settings.json");
    private readonly string _downloadsRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();

            ServiceDescriptor? descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<RuvarrDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RuvarrDbContext>(options =>
                options.UseSqlite($"Data Source={_dbPath}")
                       .UseSnakeCaseNamingConvention());

            services.RemoveAll<SettingsStore>();
            services.RemoveAll<ISettingsStore>();

            string settingsJson = JsonSerializer.Serialize(new
            {
                DownloadsRootDirectory = _downloadsRoot,
                EpisodeDownloadDirectory = Path.Combine(_downloadsRoot, "episodes"),
                MovieDownloadDirectory = Path.Combine(_downloadsRoot, "movies"),
            }, SerializerOptions);
            File.WriteAllText(_settingsPath, settingsJson);

            services.AddSingleton<SettingsStore>(_ => new SettingsStore(_settingsPath));
            services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<SettingsStore>());
        });
    }

    public async ValueTask InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        if (File.Exists(_settingsPath))
        {
            File.Delete(_settingsPath);
        }

        if (Directory.Exists(_downloadsRoot))
        {
            Directory.Delete(_downloadsRoot, recursive: true);
        }

        await base.DisposeAsync();
    }
}