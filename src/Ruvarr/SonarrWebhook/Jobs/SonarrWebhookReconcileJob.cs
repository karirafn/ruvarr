using Quartz;

namespace Ruvarr.SonarrWebhook.Jobs;

[DisallowConcurrentExecution]
internal sealed class SonarrWebhookReconcileJob(SonarrWebhookReconciler reconciler) : IJob
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    internal static readonly JobKey Key = new(nameof(SonarrWebhookReconcileJob));

    private const string ReprobeKey = "reprobe";

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await reconciler.ReconcileAsync(ModeFrom(context.MergedJobDataMap), cancellationToken);
    }

    // The interval trigger carries no data; on-demand triggers (startup, settings saved) re-probe.
    internal static JobDataMap ReprobeData() => new() { [ReprobeKey] = true };

    internal static SonarrWebhookReconcileMode ModeFrom(JobDataMap data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return data.TryGetValue(ReprobeKey, out object? reprobe) && reprobe is true
            ? SonarrWebhookReconcileMode.Reprobe
            : SonarrWebhookReconcileMode.KeepRegistered;
    }

    internal static async Task TriggerReprobeAsync(ISchedulerFactory schedulerFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedulerFactory);

        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.TriggerJob(Key, ReprobeData(), cancellationToken);
    }
}
