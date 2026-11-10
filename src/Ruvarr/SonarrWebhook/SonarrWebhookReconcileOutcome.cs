namespace Ruvarr.SonarrWebhook;

internal sealed record SonarrWebhookReconcileOutcome
{
    private SonarrWebhookReconcileOutcome(
        SonarrWebhookStatus status,
        Uri? callbackUrl,
        IReadOnlyList<Uri> triedUrls,
        DateTimeOffset attemptedAt)
    {
        Status = status;
        CallbackUrl = callbackUrl;
        TriedUrls = triedUrls;
        AttemptedAt = attemptedAt;
    }

    public SonarrWebhookStatus Status { get; }

    public Uri? CallbackUrl { get; }

    public IReadOnlyList<Uri> TriedUrls { get; }

    public DateTimeOffset AttemptedAt { get; }

    public static SonarrWebhookReconcileOutcome NotConfigured(DateTimeOffset attemptedAt) =>
        new(SonarrWebhookStatus.NotConfigured, null, [], attemptedAt);

    public static SonarrWebhookReconcileOutcome Registered(Uri callbackUrl, DateTimeOffset attemptedAt) =>
        new(SonarrWebhookStatus.Registered, callbackUrl ?? throw new ArgumentNullException(nameof(callbackUrl)), [], attemptedAt);

    public static SonarrWebhookReconcileOutcome SonarrUnreachable(DateTimeOffset attemptedAt) =>
        new(SonarrWebhookStatus.SonarrUnreachable, null, [], attemptedAt);

    public static SonarrWebhookReconcileOutcome CallbackUnreachable(IReadOnlyList<Uri> triedUrls, DateTimeOffset attemptedAt) =>
        new(SonarrWebhookStatus.CallbackUnreachable, null, triedUrls ?? throw new ArgumentNullException(nameof(triedUrls)), attemptedAt);
}
