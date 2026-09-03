
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using NSubstitute;

using Quartz;

using Ruvarr.Settings;

namespace Ruvarr.IntegrationTests;

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-settings.json");

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

            // Disable connection pooling so that EnsureDeletedAsync's implicit
            // SqliteConnection.ClearAllPools() call cannot dispose connections held
            // by other test classes running in parallel on their own databases.
            SqliteConnectionStringBuilder connectionString = new()
            {
                DataSource = _dbPath,
                Pooling = false,
            };

            services.AddDbContext<RuvarrDbContext>(options =>
                options.UseSqlite(connectionString.ToString())
                       .UseSnakeCaseNamingConvention());

            services.RemoveAll<SettingsStore>();
            services.RemoveAll<ISettingsStore>();

            string settingsJson = JsonSerializer.Serialize(new
            {
                EpisodeDownloadDirectory = "episodes",
                MovieDownloadDirectory = "movies",
            }, SerializerOptions);
            File.WriteAllText(_settingsPath, settingsJson);

            services.AddSingleton<SettingsStore>(_ => new SettingsStore(_settingsPath));
            services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<SettingsStore>());

            services.RemoveAll<ISchedulerFactory>();
            IScheduler scheduler = Substitute.For<IScheduler>();
            scheduler.GetTriggersOfJob(Arg.Any<JobKey>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ITrigger>());
            ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
            schedulerFactory.GetScheduler(Arg.Any<CancellationToken>())
                .Returns(scheduler);
            services.AddSingleton(schedulerFactory);
        });
    }

    public async ValueTask InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ISettingsStore store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await store.SaveAsync(store.Current with { IgnoredChannels = [], IgnoredPrograms = [] }, cancellationToken);
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

        await base.DisposeAsync();
    }
}