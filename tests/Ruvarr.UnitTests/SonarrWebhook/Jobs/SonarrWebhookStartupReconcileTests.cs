using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Quartz;

using Ruvarr.SonarrWebhook;
using Ruvarr.SonarrWebhook.Jobs;

namespace Ruvarr.UnitTests.SonarrWebhook.Jobs;

public sealed class SonarrWebhookStartupReconcileTests
{
    private readonly IHostApplicationLifetime _lifetime = Substitute.For<IHostApplicationLifetime>();
    private readonly ISchedulerFactory _schedulerFactory = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();

    public SonarrWebhookStartupReconcileTests()
    {
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
    }

    private SonarrWebhookStartupReconcile CreateSut(CancellationToken applicationStarted)
    {
        _lifetime.ApplicationStarted.Returns(applicationStarted);
        return new(_lifetime, _schedulerFactory, NullLogger<SonarrWebhookStartupReconcile>.Instance);
    }

    [Fact]
    public async Task TriggersReprobeReconcile_OnceHostIsListening()
    {
        // Arrange
        using CancellationTokenSource applicationStarted = new();
        SonarrWebhookStartupReconcile sut = CreateSut(applicationStarted.Token);
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Act
        await applicationStarted.CancelAsync();

        // Assert
        await _scheduler.Received(1).TriggerJob(
            Arg.Is<JobKey>(k => k.Name == nameof(SonarrWebhookReconcileJob)),
            Arg.Is<JobDataMap>(m => SonarrWebhookReconcileJob.ModeFrom(m) == SonarrWebhookReconcileMode.Reprobe),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotTrigger_BeforeHostIsListening()
    {
        // Arrange
        using CancellationTokenSource applicationStarted = new();
        SonarrWebhookStartupReconcile sut = CreateSut(applicationStarted.Token);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await _scheduler.DidNotReceive().TriggerJob(Arg.Any<JobKey>(), Arg.Any<JobDataMap>(), Arg.Any<CancellationToken>());
    }
}
