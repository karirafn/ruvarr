namespace Ruvarr.SonarrWebhook;

// In-memory like the job outcomes (ADR 0013): a still-failing registration re-fails within one
// reconcile interval after a restart, and the startup reconcile re-probes anyway.
internal sealed class SonarrWebhookHealth
{
    private readonly Lock _lock = new();
    private SonarrWebhookReconcileOutcome? _lastOutcome;
    private Uri? _registeredCallbackUrl;

    public SonarrWebhookReconcileOutcome? LastOutcome
    {
        get { lock (_lock) { return _lastOutcome; } }
    }

    // The URL of the last successful registration; survives later failures so the reconcile can
    // tell whether Sonarr's entry still points at Ruvarr.
    public Uri? RegisteredCallbackUrl
    {
        get { lock (_lock) { return _registeredCallbackUrl; } }
    }

    public void Record(SonarrWebhookReconcileOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        lock (_lock)
        {
            _lastOutcome = outcome;
            if (outcome.Status == SonarrWebhookStatus.Registered)
            {
                _registeredCallbackUrl = outcome.CallbackUrl;
            }
        }
    }
}
