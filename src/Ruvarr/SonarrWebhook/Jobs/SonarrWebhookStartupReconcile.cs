using Quartz;

namespace Ruvarr.SonarrWebhook.Jobs;

// Sonarr tests the callback URL by calling it, so the startup reconcile must wait until the
// webhook endpoint is mapped and the host is listening.
internal sealed class SonarrWebhookStartupReconcile(
    IHostApplicationLifetime lifetime,
    ISchedulerFactory schedulerFactory,
    ILogger<SonarrWebhookStartupReconcile> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => _ = TriggerAsync());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task TriggerAsync()
    {
        try
        {
            await SonarrWebhookReconcileJob.TriggerReprobeAsync(schedulerFactory, lifetime.ApplicationStopping);
        }
#pragma warning disable CA1031 // A failed trigger must not crash the host; the interval reconcile still registers the webhook.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Could not trigger the startup Sonarr webhook reconcile");
        }
    }
}
