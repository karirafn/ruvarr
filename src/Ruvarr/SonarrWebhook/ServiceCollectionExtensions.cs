using Ruvarr.Abstractions;
using Ruvarr.SonarrWebhook.Commands.ReceiveSonarrWebhook;
using Ruvarr.SonarrWebhook.Jobs;

namespace Ruvarr.SonarrWebhook;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddSonarrWebhook(this IServiceCollection services)
    {
        services.AddSingleton<SonarrWebhookHealth>();
        services.AddSingleton<ICallbackUrlCandidates, CallbackUrlCandidates>();
        services.AddTransient<SonarrWebhookReconciler>();
        services.AddTransient<IRequestHandler<ReceiveSonarrWebhookCommand>, ReceiveSonarrWebhookHandler>();
        services.AddHostedService<SonarrWebhookStartupReconcile>();

        return services;
    }
}
