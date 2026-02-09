using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using Ruvarr;

IHostBuilder builder = Host.CreateDefaultBuilder();

builder.ConfigureHostConfiguration(host => host.AddUserSecrets<Program>());

builder.ConfigureServices((context, services) =>
{
    services.AddRuvarr(context.Configuration.GetConnectionString("Default")
        ?? throw new ArgumentException("Connection string not found"));
});

IHost app = builder.Build();

await app.RunAsync()
    .ConfigureAwait(false);