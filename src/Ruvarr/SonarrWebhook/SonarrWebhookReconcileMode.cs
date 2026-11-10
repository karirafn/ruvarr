namespace Ruvarr.SonarrWebhook;

internal enum SonarrWebhookReconcileMode
{
    // Leave a registration that still matches the last registered URL alone.
    KeepRegistered,

    // Re-test the callback candidates even when the registration looks intact, because Ruvarr's
    // address may have changed (container restart) or Sonarr moved (settings saved).
    Reprobe,
}
