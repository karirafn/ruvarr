using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;

namespace Ruvarr.SonarrWebhook;

internal sealed class SonarrWebhookReconciler(
    ISettingsStore settingsStore,
    ISonarrClient sonarr,
    ICallbackUrlCandidates callbackUrlCandidates,
    SonarrWebhookHealth health,
    IDomainEventBroadcaster broadcaster,
    TimeProvider timeProvider,
    ILogger<SonarrWebhookReconciler> logger)
{
    public async Task ReconcileAsync(SonarrWebhookReconcileMode mode, CancellationToken cancellationToken)
    {
        SonarrWebhookReconcileOutcome outcome = await DetermineOutcomeAsync(mode, cancellationToken);
        health.Record(outcome);
        broadcaster.Publish(new SonarrWebhookReconciledEvent());
    }

    private async Task<SonarrWebhookReconcileOutcome> DetermineOutcomeAsync(SonarrWebhookReconcileMode mode, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        RuvarrSettings settings = settingsStore.Current;

        if (!settings.IsSonarrConfigured || !Uri.TryCreate(settings.SonarrBaseAddress, UriKind.Absolute, out Uri? sonarrBaseAddress))
        {
            logger.LogDebug("Skipping Sonarr webhook reconcile: Sonarr is not configured");
            return SonarrWebhookReconcileOutcome.NotConfigured(now);
        }

        Result<IReadOnlyList<SonarrNotification>> notifications = await sonarr.GetNotificationsAsync(cancellationToken);
        if (notifications.IsFailure)
        {
            logger.LogWarning(
                "Sonarr webhook registration failed: Sonarr is unreachable ({ErrorCode}); retrying on the next reconcile",
                notifications.Error.Code);
            return SonarrWebhookReconcileOutcome.SonarrUnreachable(now);
        }

        SonarrNotification? existing = notifications.FromResult().FirstOrDefault(RuvarrWebhookNotification.IsRuvarr);
        Uri? registeredUrl = health.RegisteredCallbackUrl;

        if (mode == SonarrWebhookReconcileMode.KeepRegistered
            && existing is not null
            && registeredUrl is not null
            && RuvarrWebhookNotification.IsRegisteredAt(existing, registeredUrl))
        {
            return SonarrWebhookReconcileOutcome.Registered(registeredUrl, now);
        }

        // Sonarr answered the list call, so a failed test means Sonarr could not reach the candidate.
        // Sonarr reports that as 400 or 500 depending on how the call failed, so status codes are not used.
        // Candidates are tested as the existing entry because Sonarr's test also checks the name is unique.
        IReadOnlyList<Uri> candidates = callbackUrlCandidates.Discover(sonarrBaseAddress);
        List<Uri> tried = [];
        Uri? callbackUrl = null;
        foreach (Uri candidate in candidates)
        {
            tried.Add(candidate);
            RuvarrResult test = await sonarr.TestNotificationAsync(RuvarrWebhookNotification.For(candidate) with { Id = existing?.Id }, cancellationToken);
            if (test.IsSuccess)
            {
                callbackUrl = candidate;
                break;
            }
        }

        if (callbackUrl is null)
        {
            logger.LogWarning(
                "Sonarr webhook registration failed: Sonarr could not reach Ruvarr at any of {TriedUrls}; retrying on the next reconcile",
                string.Join(", ", tried.Select(u => u.AbsoluteUri)));
            return SonarrWebhookReconcileOutcome.CallbackUnreachable(tried, now);
        }

        if (existing is not null && RuvarrWebhookNotification.IsRegisteredAt(existing, callbackUrl))
        {
            return SonarrWebhookReconcileOutcome.Registered(callbackUrl, now);
        }

        SonarrNotification desired = RuvarrWebhookNotification.For(callbackUrl);
        RuvarrResult saved = existing?.Id is int id
            ? await sonarr.UpdateNotificationAsync(id, desired, cancellationToken)
            : await sonarr.CreateNotificationAsync(desired, cancellationToken);

        if (saved.IsFailure)
        {
            logger.LogWarning(
                "Sonarr webhook registration failed: Sonarr is unreachable or refused to save the notification ({ErrorCode}); retrying on the next reconcile",
                saved.Error.Code);
            return SonarrWebhookReconcileOutcome.SonarrUnreachable(now);
        }

        logger.LogInformation(
            "Sonarr webhook {Action} with callback URL {CallbackUrl}",
            existing is null ? "registered" : "restored",
            callbackUrl.AbsoluteUri);
        return SonarrWebhookReconcileOutcome.Registered(callbackUrl, now);
    }
}
