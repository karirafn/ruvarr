using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Ruvarr;
using Ruvarr.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRuvarr(builder.Configuration.GetConnectionString("Default")
    ?? throw new ArgumentException("Connection string not found"));

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

app.MapStaticAssets();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
