using Quartz;

using Ruvarr.SonarrWebhook;
using Ruvarr.SonarrWebhook.Jobs;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrWebhook.Jobs;

public sealed class SonarrWebhookReconcileJobTests
{
    [Fact]
    public void ModeFrom_IsReprobe_ForReprobeTriggerData()
    {
        // Act
        SonarrWebhookReconcileMode mode = SonarrWebhookReconcileJob.ModeFrom(SonarrWebhookReconcileJob.ReprobeData());

        // Assert
        mode.ShouldBe(SonarrWebhookReconcileMode.Reprobe);
    }

    [Fact]
    public void ModeFrom_IsKeepRegistered_ForScheduledTrigger()
    {
        // Act
        SonarrWebhookReconcileMode mode = SonarrWebhookReconcileJob.ModeFrom(new JobDataMap());

        // Assert
        mode.ShouldBe(SonarrWebhookReconcileMode.KeepRegistered);
    }
}
