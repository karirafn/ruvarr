using Ruvarr;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddRuvarr(builder.Configuration.GetConnectionString("Default")
    ?? throw new ArgumentException("Connection string not found"));

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

await app.RunAsync();