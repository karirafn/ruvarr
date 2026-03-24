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

try
{
    RuvarrSettings settings = app.Services.GetRequiredService<ISettingsStore>().Current;
    Directory.CreateDirectory(RuvarrSettings.DownloadsRoot);
    Directory.CreateDirectory(settings.ResolvedEpisodeDownloadDirectory);
    Directory.CreateDirectory(settings.ResolvedMovieDownloadDirectory);
}
catch (UnauthorizedAccessException)
{
    // Download directories may not be creatable in all environments (CI, tests, containers).
}

app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
