using Ruvarr.Dashboard;
using Ruvarr.Dashboard.Queries.GetDashboard;
using Ruvarr.SonarrWebhook;

using Shouldly;

namespace Ruvarr.UnitTests.Dashboard.Queries.GetDashboardHandlerTests;

public sealed class ToSonarrWebhookCard
{
    private static readonly DateTimeOffset AttemptedAt = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NextAttemptAt = AttemptedAt.AddMinutes(10);
    private static readonly Uri CallbackUrl = new("http://172.17.0.5:8080/api/sonarr/webhook");

    [Fact]
    public void IsNotConfigured_WhenSonarrIsNotConfigured_EvenAfterEarlierRegistration()
    {
        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(
            isSonarrConfigured: false,
            SonarrWebhookReconcileOutcome.Registered(CallbackUrl, AttemptedAt),
            NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.NotConfigured);
    }

    [Fact]
    public void IsPendingWithNoLastAttempt_BeforeAnyReconcile()
    {
        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(isSonarrConfigured: true, lastOutcome: null, NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.Pending);
        card.LastAttemptAt.ShouldBeNull();
        card.CallbackUrl.ShouldBeNull();
        card.NextAttemptUtc.ShouldBe(NextAttemptAt);
    }

    [Fact]
    public void IsPending_WhenLastReconcileRanBeforeSonarrWasConfigured()
    {
        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(
            isSonarrConfigured: true,
            SonarrWebhookReconcileOutcome.NotConfigured(AttemptedAt),
            NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.Pending);
        card.LastAttemptAt.ShouldBeNull();
    }

    [Fact]
    public void IsRegisteredWithCallbackUrl_AfterSuccessfulReconcile()
    {
        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(
            isSonarrConfigured: true,
            SonarrWebhookReconcileOutcome.Registered(CallbackUrl, AttemptedAt),
            NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.Registered);
        card.CallbackUrl.ShouldBe(CallbackUrl);
        card.LastAttemptAt.ShouldBe(AttemptedAt);
        card.TriedUrls.ShouldBeEmpty();
    }

    [Fact]
    public void CarriesTriedUrls_WhenCallbackUnreachable()
    {
        // Arrange
        Uri other = new("http://ruvarr:8080/api/sonarr/webhook");

        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(
            isSonarrConfigured: true,
            SonarrWebhookReconcileOutcome.CallbackUnreachable([CallbackUrl, other], AttemptedAt),
            NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.CallbackUnreachable);
        card.TriedUrls.ShouldBe([CallbackUrl, other]);
        card.CallbackUrl.ShouldBeNull();
        card.LastAttemptAt.ShouldBe(AttemptedAt);
    }

    [Fact]
    public void IsSonarrUnreachable_WhenSonarrCouldNotBeReached()
    {
        // Act
        SonarrWebhookCardInfo card = GetDashboardHandler.ToSonarrWebhookCard(
            isSonarrConfigured: true,
            SonarrWebhookReconcileOutcome.SonarrUnreachable(AttemptedAt),
            NextAttemptAt);

        // Assert
        card.Status.ShouldBe(SonarrWebhookStatus.SonarrUnreachable);
        card.LastAttemptAt.ShouldBe(AttemptedAt);
    }
}
