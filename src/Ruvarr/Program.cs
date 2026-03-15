using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Ruvarr;
using Ruvarr.Components;
using Ruvarr.Programs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapStaticAssets();
app.UseAntiforgery();
app.MapPogramEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
