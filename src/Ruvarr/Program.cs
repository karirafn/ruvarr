using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Ruvarr;
using Ruvarr.Components;
using Ruvarr.Settings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRuvarr(
    builder.Configuration.GetConnectionString("Default")
        ?? throw new ArgumentException("Connection string not found"),
    Path.Combine("data", "settings.json"));

WebApplication app = builder.Build();

string? connectionString = builder.Configuration.GetConnectionString("Default");
if (connectionString is not null)
{
    string dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
    string? dir = Path.GetDirectoryName(dataSource);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
}

using (IServiceScope scope = app.Services.CreateScope())
{
    RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
    await dbContext.Database.MigrateAsync();
}

RuvarrSettings settings = app.Services.GetRequiredService<ISettingsStore>().Current;
Directory.CreateDirectory(settings.DownloadsRootDirectory);

string resolvedRoot = Path.GetFullPath(settings.DownloadsRootDirectory);

string resolvedEpisodeDir = Path.GetFullPath(settings.EpisodeDownloadDirectory);
if (resolvedEpisodeDir.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    || resolvedEpisodeDir == resolvedRoot)
{
    Directory.CreateDirectory(settings.EpisodeDownloadDirectory);
}

string resolvedMovieDir = Path.GetFullPath(settings.MovieDownloadDirectory);
if (resolvedMovieDir.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
    || resolvedMovieDir == resolvedRoot)
{
    Directory.CreateDirectory(settings.MovieDownloadDirectory);
}

app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
