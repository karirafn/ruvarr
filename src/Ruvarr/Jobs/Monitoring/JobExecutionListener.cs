using Quartz;

namespace Ruvarr.Jobs.Monitoring;

internal sealed class JobExecutionListener(JobOutcomeNotifier notifier) : IJobListener
{
    public string Name => nameof(JobExecutionListener);

    public ValueTask JobWasExecuted(
        IJobExecutionContext context,
        JobExecutionException? jobException,
        CancellationToken cancellationToken = default)
    {
        if (jobException is null)
        {
            notifier.RecordSuccess(context.JobDetail.Key.Name);
            return ValueTask.CompletedTask;
        }

        if (ContainsOperationCanceled(jobException))
        {
            return ValueTask.CompletedTask;
        }

        notifier.RecordFailure(context.JobDetail.Key.Name, jobException.Message);
        return ValueTask.CompletedTask;
    }

    private static bool ContainsOperationCanceled(Exception? exception)
    {
        Exception? current = exception;
        while (current is not null)
        {
            if (current is OperationCanceledException)
            {
                return true;
            }

            current = current.InnerException;
        }

        return false;
    }
}
