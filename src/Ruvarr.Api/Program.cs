using Microsoft.EntityFrameworkCore;

using Ruvarr;
using Ruvarr.Api.Programs;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddRuvarr(builder.Configuration.GetConnectionString("Default")
    ?? throw new ArgumentException("Connection string not found"));

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (IServiceScope scope = app.Services.CreateScope())
{
    RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.MapPogramEndpoints();

await app.RunAsync();