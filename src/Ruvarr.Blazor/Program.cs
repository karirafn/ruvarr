using Ruvarr.Blazor;
using Ruvarr.Blazor.Components;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddHttpClient<RuvApiClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["RuvApi:BaseAddress"]
            ?? throw new InvalidOperationException("RuvApi:BaseAddress is not configured."));
});

WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.RunAsync();
