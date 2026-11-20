using Quartz;

using Ruvarr.SonarrWebhook.Jobs;

namespace Ruvarr.SonarrFolderCheck.Jobs;

[DisallowConcurrentExecution]
internal sealed class SonarrFolderCheckJob(SonarrFolderChecker checker, SonarrFolderCheckRequeuer requeuer) : IJob
{
    internal static readonly TimeSpan Interval = SonarrWebhookReconcileJob.Interval;

    internal static readonly JobKey Key = new(nameof(SonarrFolderCheckJob));

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        SonarrFolderCheckResult result = await checker.CheckAsync(cancellationToken);
        await requeuer.RequeueAsync(result, cancellationToken);
    }

    internal static async Task TriggerAsync(ISchedulerFactory schedulerFactory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedulerFactory);

        IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        await scheduler.TriggerJob(Key, cancellationToken: cancellationToken);
    }
}
