using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;
using Ruvarr.SonarrWebhook;
using Ruvarr.Testing.Logging;
using Ruvarr.Testing.Time;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrWebhook.SonarrWebhookReconcilerTests;

public sealed class ReconcileAsync
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly Uri SonarrBaseAddress = new("http://sonarr:8989/");
    private static readonly Uri FirstCandidate = new("http://172.17.0.5:8080/api/sonarr/webhook");
    private static readonly Uri SecondCandidate = new("http://ruvarr:8080/api/sonarr/webhook");

    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ICallbackUrlCandidates _candidates = Substitute.For<ICallbackUrlCandidates>();
    private readonly SonarrWebhookHealth _health = new();
    private readonly IDomainEventBroadcaster _broadcaster = Substitute.For<IDomainEventBroadcaster>();
    private readonly RecordingLogger<SonarrWebhookReconciler> _logger = new();

    public ReconcileAsync()
    {
        _settingsStore.Current.Returns(new RuvarrSettings(SonarrBaseAddress: SonarrBaseAddress.AbsoluteUri, SonarrApiKey: "key"));
        _candidates.Discover(SonarrBaseAddress).Returns([FirstCandidate, SecondCandidate]);
        GivenSonarrHasNotifications();
        _sonarr.TestNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>()).Returns(RuvarrResult.Success);
        _sonarr.CreateNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>()).Returns(RuvarrResult.Success);
        _sonarr.UpdateNotificationAsync(Arg.Any<int>(), Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>()).Returns(RuvarrResult.Success);
    }

    private SonarrWebhookReconciler CreateSut() =>
        new(_settingsStore, _sonarr, _candidates, _health, _broadcaster, new FixedTimeProvider(Now), _logger);

    private void GivenSonarrHasNotifications(params SonarrNotification[] notifications) =>
        _sonarr.GetNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<SonarrNotification>>(notifications));

    private void GivenCandidateFailsTest(Uri candidate) =>
        _sonarr.TestNotificationAsync(Arg.Is<SonarrNotification>(n => RuvarrWebhookNotification.IsRegisteredAt(n, candidate)), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(ApiClientErrors.RequestFailed));

    private static SonarrNotification Registered(Uri callbackUrl, int id = 4) =>
        RuvarrWebhookNotification.For(callbackUrl) with { Id = id };

    private static bool IsFor(SonarrNotification notification, Uri callbackUrl) =>
        RuvarrWebhookNotification.IsRegisteredAt(notification, callbackUrl);

    [Fact]
    public async Task CreatesNotificationWithFirstCandidateThatPassesSonarrTest()
    {
        // Arrange
        GivenCandidateFailsTest(FirstCandidate);

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).CreateNotificationAsync(Arg.Is<SonarrNotification>(n => IsFor(n, SecondCandidate)), Arg.Any<CancellationToken>());
        SonarrWebhookReconcileOutcome outcome = _health.LastOutcome.ShouldNotBeNull();
        outcome.Status.ShouldBe(SonarrWebhookStatus.Registered);
        outcome.CallbackUrl.ShouldBe(SecondCandidate);
        outcome.AttemptedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task TestsCandidatesInDiscoveryOrderAndStopsAtFirstPass()
    {
        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).TestNotificationAsync(Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().TestNotificationAsync(Arg.Is<SonarrNotification>(n => IsFor(n, SecondCandidate)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNothing_WhenRegisteredNotificationIsUnchanged()
    {
        // Arrange
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);
        GivenSonarrHasNotifications(Registered(FirstCandidate));
        _sonarr.ClearReceivedCalls();

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.DidNotReceive().TestNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().CreateNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().UpdateNotificationAsync(Arg.Any<int>(), Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.Registered);
    }

    [Fact]
    public async Task RestoresNotificationWhoseTriggersDrifted()
    {
        // Arrange
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);
        GivenSonarrHasNotifications(Registered(FirstCandidate, id: 9) with { OnGrab = true });

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).UpdateNotificationAsync(9, Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.Registered);
    }

    [Fact]
    public async Task RestoresNotificationWhoseUrlDrifted()
    {
        // Arrange
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);
        GivenSonarrHasNotifications(Registered(new Uri("http://edited-by-user/"), id: 9));

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).UpdateNotificationAsync(9, Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecreatesDeletedNotification()
    {
        // Arrange
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);
        _sonarr.ClearReceivedCalls();

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).CreateNotificationAsync(Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reprobe_UpdatesUrl_WhenAddressChanged()
    {
        // Arrange — registered at the bridge IP of the previous deploy; this process has never reconciled.
        GivenSonarrHasNotifications(Registered(new Uri("http://172.17.0.9:8080/api/sonarr/webhook"), id: 3));

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.Reprobe, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).UpdateNotificationAsync(3, Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
        _health.LastOutcome!.CallbackUrl.ShouldBe(FirstCandidate);
    }

    // Sonarr validates name uniqueness on test; testing a second "Ruvarr" without the existing id fails every candidate.
    [Fact]
    public async Task TestsCandidatesAsTheExistingNotification_WhenOneExists()
    {
        // Arrange
        GivenSonarrHasNotifications(Registered(new Uri("http://172.17.0.9:8080/api/sonarr/webhook"), id: 3));

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.Reprobe, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).TestNotificationAsync(Arg.Is<SonarrNotification>(n => n.Id == 3 && IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reprobe_TestsCandidatesEvenWhenRegisteredNotificationIsUnchanged()
    {
        // Arrange
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);
        GivenSonarrHasNotifications(Registered(FirstCandidate));
        _sonarr.ClearReceivedCalls();

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.Reprobe, TestContext.Current.CancellationToken);

        // Assert
        await _sonarr.Received(1).TestNotificationAsync(Arg.Is<SonarrNotification>(n => IsFor(n, FirstCandidate)), Arg.Any<CancellationToken>());
        await _sonarr.DidNotReceive().UpdateNotificationAsync(Arg.Any<int>(), Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.Registered);
    }

    [Fact]
    public async Task RecordsSonarrUnreachable_WhenNotificationsCannotBeListed()
    {
        // Arrange
        _sonarr.GetNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<SonarrNotification>>(ApiClientErrors.RequestFailed));

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.SonarrUnreachable);
        await _sonarr.DidNotReceive().CreateNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("unreachable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecordsCallbackUnreachableWithTriedUrls_WhenNoCandidatePassesTest()
    {
        // Arrange
        GivenCandidateFailsTest(FirstCandidate);
        GivenCandidateFailsTest(SecondCandidate);

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        SonarrWebhookReconcileOutcome outcome = _health.LastOutcome.ShouldNotBeNull();
        outcome.Status.ShouldBe(SonarrWebhookStatus.CallbackUnreachable);
        outcome.TriedUrls.ShouldBe([FirstCandidate, SecondCandidate]);
        await _sonarr.DidNotReceive().CreateNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>());
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains(SecondCandidate.AbsoluteUri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecordsSonarrUnreachable_WhenCreateFails()
    {
        // Arrange
        _sonarr.CreateNotificationAsync(Arg.Any<SonarrNotification>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(ApiClientErrors.RequestFailed));

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.SonarrUnreachable);
    }

    [Fact]
    public async Task LaterSuccessClearsFailure()
    {
        // Arrange
        _sonarr.GetNotificationsAsync(Arg.Any<CancellationToken>())
            .Returns(
                new Result<IReadOnlyList<SonarrNotification>>(ApiClientErrors.RequestFailed),
                new Result<IReadOnlyList<SonarrNotification>>([]));
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.Registered);
    }

    [Fact]
    public async Task RecordsNotConfiguredWithoutCallingSonarr_WhenSonarrIsNotConfigured()
    {
        // Arrange
        _settingsStore.Current.Returns(RuvarrSettings.Empty);

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.Reprobe, TestContext.Current.CancellationToken);

        // Assert
        _health.LastOutcome!.Status.ShouldBe(SonarrWebhookStatus.NotConfigured);
        await _sonarr.DidNotReceive().GetNotificationsAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishesReconciledEvent(bool succeeds)
    {
        // Arrange
        if (!succeeds)
        {
            _sonarr.GetNotificationsAsync(Arg.Any<CancellationToken>())
                .Returns(new Result<IReadOnlyList<SonarrNotification>>(ApiClientErrors.RequestFailed));
        }

        // Act
        await CreateSut().ReconcileAsync(SonarrWebhookReconcileMode.KeepRegistered, TestContext.Current.CancellationToken);

        // Assert
        _broadcaster.Received(1).Publish(Arg.Any<SonarrWebhookReconciledEvent>());
    }
}
